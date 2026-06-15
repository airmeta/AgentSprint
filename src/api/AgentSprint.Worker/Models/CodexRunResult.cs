namespace AgentSprint.Worker.Models;

public sealed record CodexRunResult(
    string RunId,
    string Status,
    int? ExitCode,
    bool TimedOut,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string RunDirectory,
    string StdoutPath,
    string StderrPath,
    string FinalPath,
    string? Error);
