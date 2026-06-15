namespace AgentSprint.Model.Modules.Agile.Workers;

public static class DigitalWorkerTypes
{
    public const string Codex = "codex";
}

public static class DigitalWorkerEmployeeTypes
{
    public const string Operations = "operations";

    public const string Development = "development";

    public const string Audit = "audit";

    public const string Test = "test";

    public const string Product = "product";
}

public static class DigitalWorkerStatuses
{
    public const string Active = "active";

    public const string Disabled = "disabled";

    public const string Maintenance = "maintenance";
}

public static class WorkerSessionStatuses
{
    public const string Starting = "starting";

    public const string Idle = "idle";

    public const string Busy = "busy";

    public const string AuthRequired = "auth_required";

    public const string Error = "error";

    public const string Offline = "offline";

    public const string Expired = "expired";
}

public static class WorkerCommandTypes
{
    public const string Smoke = "smoke";

    public const string StartTask = "start_task";

    public const string StartBug = "start_bug";

    public const string CancelCurrentRun = "cancel_current_run";

    public const string StopAfterCurrent = "stop_after_current";

    public const string ReloadConfig = "reload_config";
}

public static class WorkerCommandStatuses
{
    public const string Pending = "pending";

    public const string Acked = "acked";

    public const string Running = "running";

    public const string Succeeded = "succeeded";

    public const string Failed = "failed";

    public const string Cancelled = "cancelled";

    public const string Expired = "expired";
}

public static class WorkerRunTypes
{
    public const string Smoke = "smoke";

    public const string Task = "task";

    public const string Bug = "bug";

    public const string Command = "command";
}

public static class WorkerRunStatuses
{
    public const string Pending = "pending";

    public const string Running = "running";

    public const string Success = "success";

    public const string CodexFailed = "codex_failed";

    public const string Timeout = "timeout";

    public const string Cancelled = "cancelled";

    public const string Blocked = "blocked";

    public const string McpFailed = "mcp_failed";
}

public static class WorkerEventLevels
{
    public const string Info = "info";

    public const string Warn = "warn";

    public const string Error = "error";
}
