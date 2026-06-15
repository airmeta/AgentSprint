namespace AgentSprint.Worker.Models;

public sealed record CommandProbeResult(
    string Command,
    string Arguments,
    int? ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut,
    string? Error)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut && string.IsNullOrWhiteSpace(Error);
}
