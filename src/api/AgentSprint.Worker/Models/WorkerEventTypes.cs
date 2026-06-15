namespace AgentSprint.Worker.Models;

public static class WorkerEventTypes
{
    public const string WorkerConfigLoading = "worker_config_loading";

    public const string WorkerConfigLoaded = "worker_config_loaded";

    public const string WorkerConfigApplied = "worker_config_applied";

    public const string WorkerEnvironmentChecked = "worker_environment_checked";

    public const string WorkerRegistered = "worker_registered";

    public const string AkkaClusterStarted = "akka_cluster_started";

    public const string HeartbeatSent = "heartbeat_sent";

    public const string CommandReceived = "command_received";

    public const string CommandAcknowledged = "command_acknowledged";

    public const string CommandStarted = "command_started";

    public const string CommandFailed = "command_failed";

    public const string WorkPromptLoading = "work_prompt_loading";

    public const string WorkPromptLoaded = "work_prompt_loaded";

    public const string WorkspacePrepareStarted = "workspace_prepare_started";

    public const string WorkspacePrepared = "workspace_prepared";

    public const string WorkspacePrepareFailed = "workspace_prepare_failed";

    public const string RunStarted = "run_started";

    public const string CodexStarted = "codex_started";

    public const string CodexFinished = "codex_finished";

    public const string WorkCompletionStarted = "work_completion_started";

    public const string WorkCompletionFinished = "work_completion_finished";

    public const string RunFinished = "run_finished";

    public const string RunBlocked = "run_blocked";

    public const string RunFailed = "run_failed";
}
