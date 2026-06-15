using System.Diagnostics;

using AgentSprint.Worker.Models;

using Air.Cloud.Core;

namespace AgentSprint.Worker.Services;

public sealed class CodexProcessRunner
{
    private static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(90);
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

        await using var stdout = new StreamWriter(paths.StdoutPath, append: false);
        await using var stderr = new StreamWriter(paths.StderrPath, append: false);

        using var watcherCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var fatalOutput = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lastOutputTicks = DateTimeOffset.UtcNow.UtcTicks;

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

        try
        {
            if (!process.Start())
            {
                error = "Codex process failed to start.";
            }
            else
            {
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

                var stdoutTask = PumpAsync(process.StandardOutput, stdout, pumpCts.Token, OnOutputLine);
                var stderrTask = PumpAsync(process.StandardError, stderr, pumpCts.Token, OnOutputLine);
                var processExitTask = process.WaitForExitAsync(cancellationToken);
                var runTimeoutTask = Task.Delay(request.Timeout, watcherCts.Token);
                var idleTimeoutTask = WatchIdleAsync(
                    idleTimeout,
                    () => new DateTimeOffset(Interlocked.Read(ref lastOutputTicks), TimeSpan.Zero),
                    watcherCts.Token);

                var completedTask = await Task.WhenAny(
                    processExitTask,
                    runTimeoutTask,
                    idleTimeoutTask,
                    fatalOutput.Task);

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
                }
                else if (completedTask == runTimeoutTask)
                {
                    timedOut = true;
                    status = "timeout";
                    error = $"Codex process timed out after {FormatDuration(request.Timeout)}.";
                    await StopProcessAsync(process, watcherCts, pumpCts);
                    await WaitForPumpsAsync(stdoutTask, stderrTask);
                }
                else if (completedTask == idleTimeoutTask)
                {
                    timedOut = true;
                    status = "timeout";
                    error = await idleTimeoutTask;
                    await StopProcessAsync(process, watcherCts, pumpCts);
                    await WaitForPumpsAsync(stdoutTask, stderrTask);
                }
                else
                {
                    status = "codex_failed";
                    error = await fatalOutput.Task;
                    await StopProcessAsync(process, watcherCts, pumpCts);
                    await WaitForPumpsAsync(stdoutTask, stderrTask);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = "cancelled";
            error = "Codex process was cancelled.";
            ProcessCommandRunner.TryKillProcessTree(process);
        }
        catch (Exception ex)
        {
            status = "codex_failed";
            error = ex.Message;
            ProcessCommandRunner.TryKillProcessTree(process);
        }

        void OnOutputLine(string line)
        {
            Interlocked.Exchange(ref lastOutputTicks, DateTimeOffset.UtcNow.UtcTicks);
            if (TryClassifyFatalOutputLine(line, out var reason))
            {
                fatalOutput.TrySetResult(reason);
            }
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

    internal static bool TryClassifyFatalOutputLine(string line, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = line.Trim();
        var lower = normalized.ToLowerInvariant();
        var hasErrorSignal =
            lower.Contains("error", StringComparison.Ordinal) ||
            lower.Contains("failed", StringComparison.Ordinal) ||
            lower.Contains("failure", StringComparison.Ordinal) ||
            lower.Contains("unauthorized", StringComparison.Ordinal) ||
            lower.Contains("forbidden", StringComparison.Ordinal) ||
            lower.Contains("invalid", StringComparison.Ordinal) ||
            lower.Contains("timeout", StringComparison.Ordinal) ||
            lower.Contains("timed out", StringComparison.Ordinal);

        if (lower.Contains("401", StringComparison.Ordinal) ||
            lower.Contains("403", StringComparison.Ordinal) ||
            lower.Contains("unauthorized", StringComparison.Ordinal) ||
            lower.Contains("forbidden", StringComparison.Ordinal) ||
            lower.Contains("invalid api key", StringComparison.Ordinal) ||
            lower.Contains("authentication", StringComparison.Ordinal) ||
            lower.Contains("not logged in", StringComparison.Ordinal) ||
            lower.Contains("access is denied", StringComparison.Ordinal))
        {
            reason = $"Codex authentication failed: {normalized}";
            return true;
        }

        if (lower.Contains("429", StringComparison.Ordinal) ||
            lower.Contains("rate limit", StringComparison.Ordinal) ||
            lower.Contains("quota", StringComparison.Ordinal))
        {
            reason = $"Codex rate limit or quota failure: {normalized}";
            return true;
        }

        if ((lower.Contains("500", StringComparison.Ordinal) ||
             lower.Contains("502", StringComparison.Ordinal) ||
             lower.Contains("503", StringComparison.Ordinal) ||
             lower.Contains("504", StringComparison.Ordinal) ||
             lower.Contains("bad gateway", StringComparison.Ordinal) ||
             lower.Contains("service unavailable", StringComparison.Ordinal) ||
             lower.Contains("gateway timeout", StringComparison.Ordinal)) &&
            hasErrorSignal)
        {
            reason = $"Codex upstream service failed: {normalized}";
            return true;
        }

        if (lower.Contains("enotfound", StringComparison.Ordinal) ||
            lower.Contains("econnrefused", StringComparison.Ordinal) ||
            lower.Contains("econnreset", StringComparison.Ordinal) ||
            lower.Contains("etimedout", StringComparison.Ordinal) ||
            lower.Contains("fetch failed", StringComparison.Ordinal) ||
            lower.Contains("network error", StringComparison.Ordinal))
        {
            reason = $"Codex network failure: {normalized}";
            return true;
        }

        return false;
    }

    private static async Task PumpAsync(
        StreamReader reader,
        StreamWriter writer,
        CancellationToken cancellationToken,
        Action<string> onLine)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            onLine(line);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
    }

    private static async Task<string> WatchIdleAsync(
        TimeSpan idleTimeout,
        Func<DateTimeOffset> getLastOutputAt,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Clamp(idleTimeout.TotalSeconds / 4, 1, 10));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var idleFor = DateTimeOffset.UtcNow - getLastOutputAt();
            if (idleFor >= idleTimeout)
            {
                return $"Codex process produced no stdout/stderr for {FormatDuration(idleTimeout)}.";
            }

            await Task.Delay(delay, cancellationToken);
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
}
