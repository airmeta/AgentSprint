using System.Diagnostics;
using System.Security.Cryptography;

using AgentSprint.Worker.Models;

using Air.Cloud.Core;

namespace AgentSprint.Worker.Services;

public sealed class CodexProcessRunner
{
    private static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan KillWaitTimeout = TimeSpan.FromSeconds(10);

    private readonly WorkerRunLogger _runLogger;

    /// <summary>
    /// <para>zh-cn:创建 Codex CLI 进程执行器。执行器负责组装 codex exec 参数、启动进程、持续写入 stdout/stderr、等待 final.md，并在结束后写入 run.json 摘要。</para>
    /// <para>en-us:Creates the Codex CLI process runner. The runner builds codex exec arguments, starts the process, continuously writes stdout/stderr, waits for final.md, and writes the run.json manifest after completion.</para>
    /// </summary>
    /// <param name="runLogger">
    /// <para>zh-cn:运行目录记录器。</para>
    /// <para>en-us:Run-directory logger.</para>
    /// </param>
    public CodexProcessRunner(WorkerRunLogger runLogger)
    {
        _runLogger = runLogger;
    }

    /// <summary>
    /// <para>zh-cn:执行一次 codex exec。方法会在超时或取消时尽力终止整个进程树；返回状态仅基于进程退出码、超时和取消信号分类，后续平台级 blocked、mcp_failed、lease_lost 可在结果解析阶段继续细分。</para>
    /// <para>en-us:Runs one codex exec invocation. The method best-effort kills the whole process tree on timeout or cancellation; the returned status is classified only by process exit code, timeout, and cancellation, leaving platform-level blocked, mcp_failed, and lease_lost refinement to result parsing.</para>
    /// </summary>
    /// <param name="request">
    /// <para>zh-cn:Codex 执行请求。</para>
    /// <para>en-us:Codex run request.</para>
    /// </param>
    /// <param name="cancellationToken">
    /// <para>zh-cn:取消令牌。</para>
    /// <para>en-us:Cancellation token.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:Codex 执行结果。</para>
    /// <para>en-us:Codex run result.</para>
    /// </returns>
    public async Task<CodexRunResult> RunAsync(CodexRunRequest request, CancellationToken cancellationToken)
    {
        var paths = await _runLogger.PrepareAsync(request.RunId, request.Prompt, cancellationToken);
        var startedAt = DateTimeOffset.UtcNow;
        string? error = null;
        int? exitCode = null;
        var timedOut = false;
        var status = "codex_failed";
        var idleTimeout = request.IdleTimeout.GetValueOrDefault(DefaultIdleTimeout);

        Directory.CreateDirectory(request.WorkingDirectory);
        WorkerDiagnostics.Info(
            "Codex运行准备完成",
            $"runId={request.RunId}, workingDirectory={request.WorkingDirectory}, runDirectory={paths.RunDirectory}, promptPath={paths.PromptPath}, stdoutPath={paths.StdoutPath}, stderrPath={paths.StderrPath}, finalPath={paths.FinalPath}, sandboxMode={request.SandboxMode}, skipGitRepoCheck={request.SkipGitRepoCheck}, timeout={request.Timeout}, idleTimeout={idleTimeout}, codexExecutable={request.CodexExecutable ?? "codex"}");

        await using var stdout = new StreamWriter(paths.StdoutPath, append: false);
        await using var stderr = new StreamWriter(paths.StderrPath, append: false);

        using var watcherCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var lastOutputTicks = DateTimeOffset.UtcNow.UtcTicks;
        var hasOutput = false;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(request.CodexExecutable) ? "codex" : request.CodexExecutable,
                WorkingDirectory = request.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
        AddCodexArguments(process.StartInfo, request, paths.FinalPath);
        WorkerDiagnostics.Info(
            "Codex进程启动参数",
            $"runId={request.RunId}, fileName={process.StartInfo.FileName}, workingDirectory={process.StartInfo.WorkingDirectory}, arguments={FormatArguments(process.StartInfo.ArgumentList)}");
        WorkerDiagnostics.Info(
            "Codex启动环境诊断",
            BuildLaunchDiagnostics(request, paths.FinalPath, idleTimeout));

        try
        {
            if (!process.Start())
            {
                error = "Codex process failed to start.";
                WorkerDiagnostics.Error("Codex进程启动失败", $"runId={request.RunId}, error={error}");
            }
            else
            {
                WorkerDiagnostics.Info(
                    "Codex进程已启动",
                    $"runId={request.RunId}, processId={process.Id}, startedAt={startedAt:O}");
                try
                {
                    AppRealization.TraceLog.Write(
                        AppRealization.JSON.Serialize(new
                        {
                            level = "Information",
                            message = "Started codex exec.",
                            runId = request.RunId
                        }),
                        new Dictionary<string, string>()
                        {
                            { "runId", request.RunId }
                        });
                }
                catch
                {
                }

                var stdoutTask = PumpAsync(process.StandardOutput, stdout, "stdout", pumpCts.Token, OnOutputLine);
                var stderrTask = PumpAsync(process.StandardError, stderr, "stderr", pumpCts.Token, OnOutputLine);
                var processExitTask = process.WaitForExitAsync(cancellationToken);
                var runTimeoutTask = Task.Delay(request.Timeout, watcherCts.Token);
                var idleTimeoutTask = WatchIdleAsync(
                    idleTimeout,
                    () => new DateTimeOffset(Interlocked.Read(ref lastOutputTicks), TimeSpan.Zero),
                    () => Volatile.Read(ref hasOutput),
                    progress => ReportProgressAsync(request, progress, CancellationToken.None),
                    watcherCts.Token);

                var completedTask = await Task.WhenAny(
                    processExitTask,
                    runTimeoutTask,
                    idleTimeoutTask);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (completedTask == processExitTask)
                {
                    await processExitTask;
                    watcherCts.Cancel();
                    await Task.WhenAll(stdoutTask, stderrTask);

                    exitCode = process.ExitCode;
                    status = exitCode == 0 && File.Exists(paths.FinalPath) ? "success" : "codex_failed";
                    WorkerDiagnostics.Info(
                        "Codex进程正常退出",
                        $"runId={request.RunId}, exitCode={exitCode}, finalExists={File.Exists(paths.FinalPath)}, status={status}");
                }
                else if (completedTask == runTimeoutTask)
                {
                    timedOut = true;
                    status = "timeout";
                    error = $"Codex process timed out after {FormatDuration(request.Timeout)}.";
                    WorkerDiagnostics.Warn("Codex进程总超时", $"runId={request.RunId}, error={error}");
                    await StopProcessAsync(process, watcherCts, pumpCts);
                    await WaitForPumpsAsync(stdoutTask, stderrTask);
                }
                else if (completedTask == idleTimeoutTask)
                {
                    timedOut = true;
                    status = "timeout";
                    error = await idleTimeoutTask;
                    WorkerDiagnostics.Warn("Codex进程空闲超时", $"runId={request.RunId}, error={error}");
                    await StopProcessAsync(process, watcherCts, pumpCts);
                    await WaitForPumpsAsync(stdoutTask, stderrTask);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = "cancelled";
            error = "Codex process was cancelled.";
            WorkerDiagnostics.Warn("Codex进程被取消", $"runId={request.RunId}, error={error}");
            ProcessCommandRunner.TryKillProcessTree(process);
        }
        catch (Exception ex)
        {
            status = "codex_failed";
            error = ex.Message;
            WorkerDiagnostics.Error("Codex进程异常", $"runId={request.RunId}, error={ex}");
            ProcessCommandRunner.TryKillProcessTree(process);
        }

        void OnOutputLine(string streamName, string line)
        {
            Interlocked.Exchange(ref lastOutputTicks, DateTimeOffset.UtcNow.UtcTicks);
            Volatile.Write(ref hasOutput, true);
            WorkerDiagnostics.Info("Codex输出", $"runId={request.RunId}, stream={streamName}, line={WorkerDiagnostics.Trim(line, 2000)}");
            _ = ReportOutputAsync(request, streamName, line, CancellationToken.None);
        }

        var completedAt = DateTimeOffset.UtcNow;
        var manifest = new WorkerRunManifest
        {
            RunId = request.RunId,
            Status = status,
            ExitCode = exitCode,
            TimedOut = timedOut,
            WorkingDirectory = request.WorkingDirectory,
            PromptPath = paths.PromptPath,
            StdoutPath = paths.StdoutPath,
            StderrPath = paths.StderrPath,
            FinalPath = paths.FinalPath,
            Error = error,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };

        await _runLogger.WriteManifestAsync(paths, manifest, CancellationToken.None);
        WorkerDiagnostics.Info(
            "Codex运行结果已写入",
            $"runId={request.RunId}, status={status}, exitCode={exitCode?.ToString() ?? "<null>"}, timedOut={timedOut}, error={error ?? string.Empty}, manifestPath={paths.ManifestPath}, stdoutPath={paths.StdoutPath}, stderrPath={paths.StderrPath}, finalPath={paths.FinalPath}, completedAt={completedAt:O}");

        return new CodexRunResult(
            request.RunId,
            status,
            exitCode,
            timedOut,
            startedAt,
            completedAt,
            paths.RunDirectory,
            paths.StdoutPath,
            paths.StderrPath,
            paths.FinalPath,
            error);
    }

    private static void AddCodexArguments(ProcessStartInfo startInfo, CodexRunRequest request, string finalPath)
    {
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--cd");
        startInfo.ArgumentList.Add(request.WorkingDirectory);
        startInfo.ArgumentList.Add("--sandbox");
        startInfo.ArgumentList.Add(request.SandboxMode);
        startInfo.ArgumentList.Add("--output-last-message");
        startInfo.ArgumentList.Add(finalPath);

        if (request.SkipGitRepoCheck)
        {
            startInfo.ArgumentList.Add("--skip-git-repo-check");
        }

        startInfo.ArgumentList.Add(request.Prompt);
    }

    private static async Task PumpAsync(
        StreamReader reader,
        StreamWriter writer,
        string streamName,
        CancellationToken cancellationToken,
        Action<string, string> onLine)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            onLine(streamName, line);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
    }

    internal static async Task<string> WatchIdleAsync(
        TimeSpan idleTimeout,
        Func<DateTimeOffset> getLastOutputAt,
        Func<bool> hasOutput,
        Func<CodexRunProgressEvent, Task> reportProgressAsync,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Clamp(idleTimeout.TotalSeconds / 4, 1, 10));
        var warningAfter = TimeSpan.FromSeconds(Math.Max(1, idleTimeout.TotalSeconds / 2));
        var warningReported = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observedAt = DateTimeOffset.UtcNow;
            var lastOutputAt = getLastOutputAt();
            var idleFor = observedAt - lastOutputAt;
            if (!warningReported && idleFor >= warningAfter)
            {
                warningReported = true;
                WorkerDiagnostics.Warn(
                    "Codex进程长时间无输出",
                    FormatIdleDiagnostics(WorkerEventTypes.CodexIdleWaiting, observedAt, lastOutputAt, idleFor, idleTimeout, hasOutput()));
                await reportProgressAsync(new CodexRunProgressEvent(
                    WorkerEventTypes.CodexIdleWaiting,
                    "warn",
                    "Codex process is still running but has not produced stdout/stderr recently.",
                    observedAt,
                    lastOutputAt,
                    idleFor,
                    idleTimeout,
                    hasOutput()));
            }

            if (idleFor >= idleTimeout)
            {
                WorkerDiagnostics.Warn(
                    "Codex进程空闲超时诊断",
                    FormatIdleDiagnostics(WorkerEventTypes.CodexIdleTimeout, observedAt, lastOutputAt, idleFor, idleTimeout, hasOutput()));
                await reportProgressAsync(new CodexRunProgressEvent(
                    WorkerEventTypes.CodexIdleTimeout,
                    "error",
                    "Codex process reached stdout/stderr idle timeout.",
                    observedAt,
                    lastOutputAt,
                    idleFor,
                    idleTimeout,
                    hasOutput()));
                return $"Codex process produced no stdout/stderr for {FormatDuration(idleTimeout)}.";
            }

            await Task.Delay(delay, cancellationToken);
        }
    }

    internal static string BuildLaunchDiagnostics(
        CodexRunRequest request,
        string finalPath,
        TimeSpan idleTimeout)
    {
        var rawCodexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var codexHome = NormalizeForLog(rawCodexHome);
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var agentToken = Environment.GetEnvironmentVariable("AGENTSPRINT_AGENT_TOKEN");
        var configPath = string.IsNullOrWhiteSpace(rawCodexHome)
            ? string.Empty
            : Path.Combine(rawCodexHome.Trim(), "config.toml");
        var config = InspectCodexConfig(configPath);

        return string.Join(
            ", ",
            $"runId={request.RunId}",
            $"codexExecutable={request.CodexExecutable ?? "codex"}",
            $"workingDirectory={request.WorkingDirectory}",
            $"workingDirectoryExists={Directory.Exists(request.WorkingDirectory)}",
            $"finalPath={finalPath}",
            $"promptLength={request.Prompt.Length}",
            $"sandboxMode={request.SandboxMode}",
            $"skipGitRepoCheck={request.SkipGitRepoCheck}",
            $"timeout={FormatDuration(request.Timeout)}",
            $"idleTimeout={FormatDuration(idleTimeout)}",
            $"processCODEX_HOME={codexHome}",
            $"configPath={configPath}",
            $"configExists={config.Exists}",
            $"configLength={config.Length?.ToString() ?? "<null>"}",
            $"configLastWriteUtc={config.LastWriteUtc ?? "<null>"}",
            $"configSha256={config.Sha256Prefix ?? "<null>"}",
            $"configModel={config.Model ?? "<null>"}",
            $"configProvider={config.Provider ?? "<null>"}",
            $"configBaseUrl={config.BaseUrl ?? "<null>"}",
            $"configReadError={config.ReadError ?? "<null>"}",
            $"hasOPENAI_API_KEY={!string.IsNullOrWhiteSpace(openAiApiKey)}",
            $"hasAGENTSPRINT_AGENT_TOKEN={!string.IsNullOrWhiteSpace(agentToken)}",
            $"hasHTTP_PROXY={!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HTTP_PROXY"))}",
            $"hasHTTPS_PROXY={!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HTTPS_PROXY"))}",
            $"hasNO_PROXY={!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NO_PROXY"))}");
    }

    private static CodexConfigDiagnostics InspectCodexConfig(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return new CodexConfigDiagnostics(false, null, null, null, null, null, null, null);
        }

        try
        {
            if (!File.Exists(configPath))
            {
                return new CodexConfigDiagnostics(false, null, null, null, null, null, null, null);
            }

            var bytes = File.ReadAllBytes(configPath);
            var lines = File.ReadAllLines(configPath);
            return new CodexConfigDiagnostics(
                true,
                bytes.LongLength,
                File.GetLastWriteTimeUtc(configPath).ToString("O"),
                Convert.ToHexString(SHA256.HashData(bytes))[..12],
                ReadTomlValue(lines, "model"),
                ReadTomlValue(lines, "model_provider"),
                ReadTomlValue(lines, "base_url"),
                null);
        }
        catch (Exception ex)
        {
            return new CodexConfigDiagnostics(
                File.Exists(configPath),
                null,
                null,
                null,
                null,
                null,
                null,
                ex.Message);
        }
    }

    private static string? ReadTomlValue(IReadOnlyCollection<string> lines, string key)
    {
        var prefix = key + " = ";
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            return trimmed[prefix.Length..].Trim().Trim('"');
        }

        return null;
    }

    private static string FormatIdleDiagnostics(
        string eventType,
        DateTimeOffset observedAt,
        DateTimeOffset lastOutputAt,
        TimeSpan idleFor,
        TimeSpan idleTimeout,
        bool hasOutput)
    {
        return string.Join(
            ", ",
            $"eventType={eventType}",
            $"observedAt={observedAt:O}",
            $"lastOutputAt={lastOutputAt:O}",
            $"idleFor={FormatDuration(idleFor)}",
            $"idleTimeout={FormatDuration(idleTimeout)}",
            $"hasOutput={hasOutput}");
    }

    private static string NormalizeForLog(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();
    }

    private static async Task ReportProgressAsync(
        CodexRunRequest request,
        CodexRunProgressEvent progress,
        CancellationToken cancellationToken)
    {
        if (request.ProgressReporter is null)
        {
            return;
        }

        try
        {
            await request.ProgressReporter(progress, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WorkerDiagnostics.Warn(
                "Codex运行进度上报失败",
                $"runId={request.RunId}, eventType={progress.EventType}, error={ex.Message}");
        }
    }

    private static async Task ReportOutputAsync(
        CodexRunRequest request,
        string streamName,
        string line,
        CancellationToken cancellationToken)
    {
        if (request.OutputReporter is null)
        {
            return;
        }

        try
        {
            await request.OutputReporter(streamName, line, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WorkerDiagnostics.Warn(
                "Codex杩愯杈撳嚭涓婃姤澶辫触",
                $"runId={request.RunId}, stream={streamName}, error={ex.Message}");
        }
    }

    private static async Task StopProcessAsync(
        Process process,
        CancellationTokenSource watcherCts,
        CancellationTokenSource pumpCts)
    {
        watcherCts.Cancel();
        ProcessCommandRunner.TryKillProcessTree(process);

        using var killWaitCts = new CancellationTokenSource(KillWaitTimeout);
        try
        {
            await process.WaitForExitAsync(killWaitCts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        pumpCts.Cancel();
    }

    private static async Task WaitForPumpsAsync(params Task[] pumpTasks)
    {
        try
        {
            await Task.WhenAll(pumpTasks);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.TotalMinutes:0.#} minute(s)";
        }

        return $"{duration.TotalSeconds:0.#} second(s)";
    }

    private static string FormatArguments(System.Collections.ObjectModel.Collection<string> arguments)
    {
        return string.Join(" ", arguments.Select(argument =>
            argument.Contains(' ', StringComparison.Ordinal) || argument.Contains('"', StringComparison.Ordinal)
                ? "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
                : argument));
    }

    private sealed record CodexConfigDiagnostics(
        bool Exists,
        long? Length,
        string? LastWriteUtc,
        string? Sha256Prefix,
        string? Model,
        string? Provider,
        string? BaseUrl,
        string? ReadError);
}
