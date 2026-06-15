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
        WorkerDiagnostics.Info(
            "Worker运行配置开始应用",
            $"workerId={config.WorkerId}, workerCode={config.WorkerCode}, workerName={config.WorkerName}, projectCode={config.ProjectCode ?? string.Empty}, workspaceRoot={config.WorkspaceRoot}, runsRoot={config.RunsRoot}, codexHome={config.CodexHome}, sandboxMode={config.SandboxMode}, codexProvider={config.CodexProvider}, codexModel={config.CodexModel}, openAiBaseUrl={config.OpenAiBaseUrl ?? string.Empty}, configVersion={config.ConfigVersion}, hasOpenAiApiKey={!string.IsNullOrWhiteSpace(config.OpenAiApiKey)}, hasAgentToken={!string.IsNullOrWhiteSpace(config.AgentToken)}");

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
        var runtimeApiKey = NormalizeOptional(config.OpenAiApiKey);
        var containerApiKey = NormalizeOptional(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        _options.OpenAiApiKey = runtimeApiKey ?? containerApiKey ?? _options.OpenAiApiKey;
        _options.ConfigVersion = config.ConfigVersion <= 0 ? _options.ConfigVersion : config.ConfigVersion;

        Directory.CreateDirectory(_options.WorkspaceRoot);
        Directory.CreateDirectory(_options.RunsRoot);
        Directory.CreateDirectory(_options.CodexHome);
        Environment.SetEnvironmentVariable("CODEX_HOME", _options.CodexHome);
        await WriteCodexConfigAsync(config.AgentToken, cancellationToken);
        WorkerDiagnostics.Info(
            "Worker运行配置应用完成",
            $"workerId={_options.WorkerId}, workspaceRoot={_options.WorkspaceRoot}, runsRoot={_options.RunsRoot}, codexHome={_options.CodexHome}, configPath={Path.Combine(_options.CodexHome, "config.toml")}, configVersion={_options.ConfigVersion}");

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
        WorkerDiagnostics.Info(
            "Codex配置写入开始",
            $"configPath={configPath}, tempPath={tempPath}, codexProvider={_options.CodexProvider}, codexModel={_options.CodexModel}, openAiBaseUrl={_options.OpenAiBaseUrl ?? string.Empty}, hasOpenAiApiKey={!string.IsNullOrWhiteSpace(_options.OpenAiApiKey)}, hasContainerOpenAiApiKey={!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))}, hasAgentToken={!string.IsNullOrWhiteSpace(agentToken)}");
        await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, cancellationToken);
        File.Move(tempPath, configPath, overwrite: true);
        WorkerDiagnostics.Info("Codex配置写入完成", $"configPath={configPath}, length={content.Length}");
    }

    internal string BuildCodexConfig(string? agentToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"model = \"{EscapeToml(_options.CodexModel)}\"");
        builder.AppendLine("model_provider = \"agentsprint\"");
        builder.AppendLine($"sandbox_mode = \"{EscapeToml(_options.SandboxMode)}\"");
        builder.AppendLine();
        builder.AppendLine("[model_providers.agentsprint]");
        builder.AppendLine($"name = \"{EscapeToml(_options.CodexProvider)}\"");
        if (!string.IsNullOrWhiteSpace(_options.OpenAiBaseUrl))
        {
            builder.AppendLine($"base_url = \"{EscapeToml(_options.OpenAiBaseUrl)}\"");
        }

        builder.AppendLine("env_key = \"OPENAI_API_KEY\"");
        builder.AppendLine("wire_api = \"responses\"");
        if (!string.IsNullOrWhiteSpace(_options.OpenAiApiKey))
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", _options.OpenAiApiKey);
            WorkerDiagnostics.Info("OPENAI_API_KEY已写入Worker进程环境", "source=runtime-config-or-container, hasOpenAiApiKey=True");
        }
        else
        {
            WorkerDiagnostics.Warn("OPENAI_API_KEY缺失", "Runtime config did not include openAiApiKey and container environment variable OPENAI_API_KEY is empty.");
        }

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
