namespace AgentSprint.Worker.Models;

public sealed class WorkerRunManifest
{
    public string RunId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int? ExitCode { get; set; }

    public bool TimedOut { get; set; }

    public string WorkingDirectory { get; set; } = string.Empty;

    public string PromptPath { get; set; } = string.Empty;

    public string StdoutPath { get; set; } = string.Empty;

    public string StderrPath { get; set; } = string.Empty;

    public string FinalPath { get; set; } = string.Empty;

    public string? Error { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }
}
