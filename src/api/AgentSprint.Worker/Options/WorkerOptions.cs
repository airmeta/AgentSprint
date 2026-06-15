namespace AgentSprint.Worker.Options;

public sealed class WorkerOptions
{
    public string WorkerId { get; set; } = "worker-1";

    public string WorkerName { get; set; } = "agentsprint-codex-worker-1";

    public string? ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string WorkspaceRoot { get; set; } = "/workspaces";

    public string RunsRoot { get; set; } = "/runs";

    public string CodexHome { get; set; } = "/codex-home";

    public int PollIntervalSeconds { get; set; } = 15;

    public int IdleMaxIntervalSeconds { get; set; } = 180;

    public int MaxRunMinutes { get; set; } = 60;

    public int CodexIdleTimeoutSeconds { get; set; } = 90;

    public string CodexExecutable { get; set; } = "codex";

    public string SandboxMode { get; set; } = "workspace-write";

    public bool RunSmokeOnStartup { get; set; }

    public string SmokePrompt { get; set; } = "你好";

    public string CodexProvider { get; set; } = "openai";

    public string CodexModel { get; set; } = "gpt-5.4";

    public string? OpenAiBaseUrl { get; set; }

    public int ConfigVersion { get; set; } = 1;
}
