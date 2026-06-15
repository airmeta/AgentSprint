namespace AgentSprint.Worker.Options;

public sealed class AgentSprintOptions
{
    public string ApiBaseUrl { get; set; } = "http://api:5000";

    public string? AgentToken { get; set; }

    public bool PullRuntimeConfigOnStartup { get; set; } = true;
}
