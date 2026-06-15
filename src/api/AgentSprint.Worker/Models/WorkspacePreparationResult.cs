namespace AgentSprint.Worker.Models;

public sealed record WorkspacePreparationResult(
    bool Succeeded,
    string WorkspacePath,
    bool RepositoryAvailable,
    string? RepositoryUrl,
    string? Branch,
    string? Commit,
    bool Dirty,
    string? Error);
