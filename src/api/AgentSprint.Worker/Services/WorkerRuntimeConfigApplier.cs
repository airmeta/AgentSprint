using System.Text;

using AgentSprint.Worker.Models;
using AgentSprint.Worker.Options;

using Air.Cloud.Core;

using Microsoft.Extensions.Options;

namespace AgentSprint.Worker.Services;

public sealed class WorkerRuntimeConfigApplier
{
    private readonly WorkerOptions _options;

    /// <summary>
    /// <para>zh-cn: 创建 Worker 平台托管配置应用器，负责把主平台返回的运行配置合并到内存选项，并生成 Codex CLI 使用的 config.toml。</para>
    /// <para>en-us: Creates the Worker platform-managed configuration applier, which merges runtime configuration returned by the main platform into in-memory options and generates the config.toml used by the Codex CLI.</para>
    /// </summary>
    /// <param name="options">
    /// <para>zh-cn: Worker 运行选项；启动时会被平台配置覆盖，未返回的字段保留本地默认值。</para>
    /// <para>en-us: Worker runtime options; startup platform configuration overrides these values while missing fields keep local defaults.</para>
    /// </param>
    public WorkerRuntimeConfigApplier(IOptions<WorkerOptions> options)
    {
        _options = options.Value;
    }

    public async Task ApplyAsync(WorkerRuntimeConfigResult config, CancellationToken cancellationToken)
    {
        _options.WorkerId = config.WorkerId;
        _options.WorkerName = config.WorkerName;
        _options.ProjectId = config.ProjectId;
        _options.ProjectCode = config.ProjectCode;
        _options.WorkspaceRoot = Normalize(config.WorkspaceRoot, _options.WorkspaceRoot);
        _options.RunsRoot = Normalize(config.RunsRoot, _options.RunsRoot);
        _options.CodexHome = Normalize(config.CodexHome, _options.CodexHome);
        _options.PollIntervalSeconds = Positive(config.PollIntervalSeconds, _options.PollIntervalSeconds);
        _options.IdleMaxIntervalSeconds = Positive(config.IdleMaxIntervalSeconds, _options.IdleMaxIntervalSeconds);
        _options.MaxRunMinutes = Positive(config.MaxRunMinutes, _options.MaxRunMinutes);
        _options.SandboxMode = Normalize(config.SandboxMode, _options.SandboxMode);
        _options.RunSmokeOnStartup = config.RunSmokeOnStartup;
        _options.SmokePrompt = Normalize(config.SmokePrompt, _options.SmokePrompt);
        _options.CodexProvider = Normalize(config.CodexProvider, _options.CodexProvider);
        _options.CodexModel = Normalize(config.CodexModel, _options.CodexModel);
        _options.OpenAiBaseUrl = NormalizeOptional(config.OpenAiBaseUrl);
        _options.ConfigVersion = config.ConfigVersion <= 0 ? _options.ConfigVersion : config.ConfigVersion;

        Directory.CreateDirectory(_options.WorkspaceRoot);
        Directory.CreateDirectory(_options.RunsRoot);
        Directory.CreateDirectory(_options.CodexHome);
        await WriteCodexConfigAsync(config.AgentToken, cancellationToken);

        try
        {
            AppRealization.TraceLog.Write(
                AppRealization.JSON.Serialize(new
                {
                    level = "Information",
                    message = "Applied Worker runtime config.",
                    configVersion = _options.ConfigVersion,
                    codexHome = _options.CodexHome,
                    workspaceRoot = _options.WorkspaceRoot
                }),
                new Dictionary<string, string>()
                {
                    { "configVersion", _options.ConfigVersion.ToString() },
                    { "codexHome", _options.CodexHome },
                    { "workspaceRoot", _options.WorkspaceRoot }
                });
        }
        catch
        {
        }
    }

    private async Task WriteCodexConfigAsync(string? agentToken, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(_options.CodexHome, "config.toml");
        var tempPath = configPath + ".tmp";
        var content = BuildCodexConfig(agentToken);
        await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, cancellationToken);
        File.Move(tempPath, configPath, overwrite: true);
    }

    internal string BuildCodexConfig(string? agentToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"model = \"{EscapeToml(_options.CodexModel)}\"");
        builder.AppendLine($"sandbox_mode = \"{EscapeToml(_options.SandboxMode)}\"");
        builder.AppendLine();
        builder.AppendLine("[model_providers.agentsprint]");
        builder.AppendLine($"name = \"{EscapeToml(_options.CodexProvider)}\"");
        if (!string.IsNullOrWhiteSpace(_options.OpenAiBaseUrl))
        {
            builder.AppendLine($"base_url = \"{EscapeToml(_options.OpenAiBaseUrl)}\"");
        }

        builder.AppendLine("bearer_token_env_var = \"AGENTSPRINT_AGENT_TOKEN\"");
        if (!string.IsNullOrWhiteSpace(agentToken))
        {
            Environment.SetEnvironmentVariable("AGENTSPRINT_AGENT_TOKEN", agentToken);
        }

        return builder.ToString();
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int Positive(int value, int fallback)
    {
        return value > 0 ? value : fallback;
    }

    private static string EscapeToml(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
