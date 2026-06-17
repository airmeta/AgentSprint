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

public sealed record WorkspacePublishResult(
    bool Succeeded,
    string WorkspacePath,
    bool HasChanges,
    bool Pushed,
    bool ConflictResolved,
    string? Branch,
    string? Commit,
    string? ChangedFilesJson,
    string? Error);

public sealed record GitConflictResolutionRequest(
    string WorkspacePath,
    string Branch,
    IReadOnlyList<string> ConflictFiles,
    string Operation,
    string Error);

public sealed record GitConflictResolutionResult(
    bool Succeeded,
    string? Error);
