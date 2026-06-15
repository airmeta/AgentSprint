using AgentSprint.Worker.Models;
using AgentSprint.Worker.Options;

using Air.Cloud.Core;

using Microsoft.Extensions.Options;

namespace AgentSprint.Worker.Services;

public sealed class WorkerEnvironmentProbe
{
    private readonly WorkerOptions _options;

    /// <summary>
    /// <para>zh-cn:创建数字员工运行环境探针。探针会检查基础目录、Codex 配置文件以及 codex、git、dotnet、node 等命令是否可用，并将结果汇总为启动快照。</para>
    /// <para>en-us:Creates the digital-worker runtime environment probe. The probe checks base directories, the Codex config file, and availability of codex, git, dotnet, and node commands, then summarizes the result as a startup snapshot.</para>
    /// </summary>
    /// <param name="options">
    /// <para>zh-cn:Worker 运行配置。</para>
    /// <para>en-us:Worker runtime options.</para>
    /// </param>
    public WorkerEnvironmentProbe(IOptions<WorkerOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// <para>zh-cn:执行一次启动环境检查。方法会自动创建工作区根目录和运行根目录，但不会创建 CodexHome/config.toml；缺少该文件会体现在快照中，供主循环进入 degraded 或 auth_required 状态。</para>
    /// <para>en-us:Runs one startup environment check. The method creates the workspace root and runs root automatically, but does not create CodexHome/config.toml; a missing file is reported in the snapshot so the main loop can enter degraded or auth_required state.</para>
    /// </summary>
    /// <param name="cancellationToken">
    /// <para>zh-cn:取消令牌。</para>
    /// <para>en-us:Cancellation token.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:环境检查快照。</para>
    /// <para>en-us:Environment-check snapshot.</para>
    /// </returns>
    public async Task<WorkerEnvironmentSnapshot> ProbeAsync(CancellationToken cancellationToken)
    {
        var codexHome = Path.GetFullPath(_options.CodexHome);
        var workspaceRoot = Path.GetFullPath(_options.WorkspaceRoot);
        var runsRoot = Path.GetFullPath(_options.RunsRoot);

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(runsRoot);

        var configTomlExists = File.Exists(Path.Combine(codexHome, "config.toml"));
        if (!configTomlExists)
        {
            var configPath = Path.Combine(codexHome, "config.toml");
            try
            {
                AppRealization.TraceLog.Write(
                    AppRealization.JSON.Serialize(new
                    {
                        level = "Warning",
                        message = "Codex config file is missing.",
                        configPath
                    }),
                    new Dictionary<string, string>()
                    {
                        { "configPath", configPath }
                    });
            }
            catch
            {
            }
        }

        var commandTimeout = TimeSpan.FromSeconds(20);
        var codexVersion = await ProcessCommandRunner.RunAsync("codex", "--version", null, commandTimeout, cancellationToken);
        var gitVersion = await ProcessCommandRunner.RunAsync("git", "--version", null, commandTimeout, cancellationToken);
        var dotnetVersion = await ProcessCommandRunner.RunAsync("dotnet", "--version", null, commandTimeout, cancellationToken);
        var nodeVersion = await ProcessCommandRunner.RunAsync("node", "--version", null, commandTimeout, cancellationToken);
        var codexLoginStatus = await ProcessCommandRunner.RunAsync("codex", "login status", null, commandTimeout, cancellationToken);

        LogProbeResult("codex --version", codexVersion);
        LogProbeResult("git --version", gitVersion);
        LogProbeResult("dotnet --version", dotnetVersion);
        LogProbeResult("node --version", nodeVersion);
        LogProbeResult("codex login status", codexLoginStatus);

        return new WorkerEnvironmentSnapshot(
            codexVersion,
            gitVersion,
            dotnetVersion,
            nodeVersion,
            codexLoginStatus,
            configTomlExists,
            codexHome,
            workspaceRoot,
            runsRoot);
    }

    private void LogProbeResult(string name, CommandProbeResult result)
    {
        if (result.Succeeded)
        {
            var output = TrimForLog(result.Stdout);
            try
            {
                AppRealization.TraceLog.Write(
                    AppRealization.JSON.Serialize(new
                    {
                        level = "Information",
                        message = "Worker environment probe succeeded.",
                        probeName = name,
                        output
                    }),
                    new Dictionary<string, string>()
                    {
                        { "probeName", name },
                        { "output", output }
                    });
            }
            catch
            {
            }

            return;
        }

        var stderr = TrimForLog(result.Stderr);
        try
        {
            AppRealization.TraceLog.Write(
                AppRealization.JSON.Serialize(new
                {
                    level = "Warning",
                    message = "Worker environment probe failed.",
                    probeName = name,
                    exitCode = result.ExitCode,
                    timedOut = result.TimedOut,
                    error = result.Error,
                    stderr
                }),
                new Dictionary<string, string>()
                {
                    { "probeName", name },
                    { "exitCode", result.ExitCode?.ToString() ?? "<null>" },
                    { "timedOut", result.TimedOut.ToString() },
                    { "error", result.Error ?? "<null>" },
                    { "stderr", stderr }
                });
        }
        catch
        {
        }
    }

    private static string TrimForLog(string value)
    {
        value = value.Trim();
        return value.Length <= 500 ? value : value[..500];
    }
}
