using AgentSprint.Worker.Models;
using AgentSprint.Worker.Options;

using Microsoft.Extensions.Options;

namespace AgentSprint.Worker.Services;

public sealed class CodexLoginInitializer
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(20);

    private readonly WorkerOptions _options;

    public CodexLoginInitializer(IOptions<WorkerOptions> options)
    {
        _options = options.Value;
    }

    public async Task EnsureLoggedInAsync(WorkerRuntimeConfigResult config, CancellationToken cancellationToken)
    {
        var platformApiKey = Normalize(config.OpenAiApiKey);
        if (platformApiKey is null)
        {
            WorkerDiagnostics.Warn(
                "Codex登录初始化跳过",
                "runtime config did not return openAiApiKey; startup probe will report current Codex login status.");
            return;
        }

        var secretValues = new[] { platformApiKey };
        Directory.CreateDirectory(_options.CodexHome);
        Environment.SetEnvironmentVariable("CODEX_HOME", _options.CodexHome);

        var status = await ProcessCommandRunner.RunAsync(
            _options.CodexExecutable,
            "login status",
            null,
            StatusTimeout,
            secretValues,
            cancellationToken);
        if (status.Succeeded)
        {
            WorkerDiagnostics.Info(
                "Codex登录状态已就绪",
                $"codexHome={_options.CodexHome}, codexExecutable={_options.CodexExecutable}");
            return;
        }

        WorkerDiagnostics.Info(
            "Codex登录初始化开始",
            $"codexHome={_options.CodexHome}, codexExecutable={_options.CodexExecutable}, hasPlatformOpenAiApiKey=True");
        var login = await ProcessCommandRunner.RunAsync(
            _options.CodexExecutable,
            "login --with-api-key",
            null,
            LoginTimeout,
            platformApiKey + Environment.NewLine,
            secretValues,
            cancellationToken);
        if (!login.Succeeded)
        {
            WorkerDiagnostics.Warn(
                "Codex登录初始化失败",
                $"exitCode={login.ExitCode?.ToString() ?? "<null>"}, timedOut={login.TimedOut}, error={WorkerDiagnostics.TrimAndRedact(login.Error, secretValues)}, stderr={WorkerDiagnostics.TrimAndRedact(login.Stderr, secretValues)}");
            return;
        }

        var verified = await ProcessCommandRunner.RunAsync(
            _options.CodexExecutable,
            "login status",
            null,
            StatusTimeout,
            secretValues,
            cancellationToken);
        WorkerDiagnostics.Info(
            verified.Succeeded ? "Codex登录初始化完成" : "Codex登录初始化后状态异常",
            $"codexHome={_options.CodexHome}, exitCode={verified.ExitCode?.ToString() ?? "<null>"}, stderr={WorkerDiagnostics.TrimAndRedact(verified.Stderr, secretValues)}");
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
