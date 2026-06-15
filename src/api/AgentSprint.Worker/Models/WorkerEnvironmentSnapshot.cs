namespace AgentSprint.Worker.Models;

public sealed record WorkerEnvironmentSnapshot(
    CommandProbeResult CodexVersion,
    CommandProbeResult GitVersion,
    CommandProbeResult DotnetVersion,
    CommandProbeResult NodeVersion,
    CommandProbeResult CodexLoginStatus,
    bool ConfigTomlExists,
    string CodexHome,
    string WorkspaceRoot,
    string RunsRoot)
{
    public bool CanEnterWorkLoop => CodexVersion.Succeeded;

    public bool IsCodexAuthenticated => CodexLoginStatus.Succeeded;
}
