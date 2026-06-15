namespace AgentSprint.Worker.Models;

public sealed record CodexRunRequest(
    string RunId,
    string WorkingDirectory,
    string Prompt,
    string SandboxMode,
    bool SkipGitRepoCheck,
    TimeSpan Timeout,
    TimeSpan? IdleTimeout = null,
    string? CodexExecutable = null);
