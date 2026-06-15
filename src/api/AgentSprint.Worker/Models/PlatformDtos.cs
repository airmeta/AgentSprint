using System.Text.Json.Serialization;

namespace AgentSprint.Worker.Models;

public sealed record ApiResponse<T>(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("data")] T? Data,
    [property: JsonPropertyName("message")] string Message);

public sealed record RegisterWorkerSessionRequest(
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("instanceId")] string InstanceId,
    [property: JsonPropertyName("hostName")] string? HostName,
    [property: JsonPropertyName("containerId")] string? ContainerId,
    [property: JsonPropertyName("codexVersion")] string? CodexVersion,
    [property: JsonPropertyName("gitVersion")] string? GitVersion,
    [property: JsonPropertyName("dotnetVersion")] string? DotnetVersion,
    [property: JsonPropertyName("nodeVersion")] string? NodeVersion,
    [property: JsonPropertyName("configTomlExists")] bool ConfigTomlExists,
    [property: JsonPropertyName("codexHome")] string? CodexHome,
    [property: JsonPropertyName("workspaceRoot")] string? WorkspaceRoot,
    [property: JsonPropertyName("runsRoot")] string? RunsRoot,
    [property: JsonPropertyName("errorSummary")] string? ErrorSummary);

public sealed record WorkerHeartbeatRequest(
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("currentRunId")] string? CurrentRunId,
    [property: JsonPropertyName("errorSummary")] string? ErrorSummary);

public sealed record AckWorkerCommandRequest(
    [property: JsonPropertyName("sessionId")] string SessionId);

public sealed record StartWorkerRunRequest(
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("runType")] string RunType,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("commandId")] string? CommandId,
    [property: JsonPropertyName("targetType")] string? TargetType,
    [property: JsonPropertyName("targetId")] string? TargetId,
    [property: JsonPropertyName("workspacePath")] string? WorkspacePath,
    [property: JsonPropertyName("promptPath")] string? PromptPath,
    [property: JsonPropertyName("stdoutPath")] string? StdoutPath,
    [property: JsonPropertyName("stderrPath")] string? StderrPath,
    [property: JsonPropertyName("finalPath")] string? FinalPath,
    [property: JsonPropertyName("manifestPath")] string? ManifestPath);

public sealed record FinishWorkerRunRequest(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("exitCode")] int? ExitCode,
    [property: JsonPropertyName("timedOut")] bool TimedOut,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("resultJson")] string? ResultJson);

public sealed record ReportWorkerEventRequest(
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("runId")] string? RunId,
    [property: JsonPropertyName("level")] string? Level,
    [property: JsonPropertyName("payloadJson")] string? PayloadJson);

public sealed record WorkerSessionResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("status")] string Status);

public sealed record WorkerCommandResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("commandType")] string CommandType,
    [property: JsonPropertyName("payloadJson")] string? PayloadJson,
    [property: JsonPropertyName("status")] string Status);

public sealed record WorkerHeartbeatResult(
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("nextIntervalSeconds")] int NextIntervalSeconds,
    [property: JsonPropertyName("commands")] IReadOnlyList<WorkerCommandResult> Commands);

public sealed record WorkerRuntimeConfigResult(
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("workerCode")] string WorkerCode,
    [property: JsonPropertyName("workerName")] string WorkerName,
    [property: JsonPropertyName("projectId")] string? ProjectId,
    [property: JsonPropertyName("projectCode")] string? ProjectCode,
    [property: JsonPropertyName("workspaceRoot")] string WorkspaceRoot,
    [property: JsonPropertyName("runsRoot")] string RunsRoot,
    [property: JsonPropertyName("codexHome")] string CodexHome,
    [property: JsonPropertyName("pollIntervalSeconds")] int PollIntervalSeconds,
    [property: JsonPropertyName("idleMaxIntervalSeconds")] int IdleMaxIntervalSeconds,
    [property: JsonPropertyName("maxRunMinutes")] int MaxRunMinutes,
    [property: JsonPropertyName("sandboxMode")] string SandboxMode,
    [property: JsonPropertyName("runSmokeOnStartup")] bool RunSmokeOnStartup,
    [property: JsonPropertyName("smokePrompt")] string SmokePrompt,
    [property: JsonPropertyName("codexProvider")] string CodexProvider,
    [property: JsonPropertyName("codexModel")] string CodexModel,
    [property: JsonPropertyName("openAiBaseUrl")] string? OpenAiBaseUrl,
    [property: JsonPropertyName("agentToken")] string? AgentToken,
    [property: JsonPropertyName("configVersion")] int ConfigVersion);

public sealed record WorkerRunResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("status")] string Status);

public sealed record WorkerEventResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("eventType")] string EventType);

public sealed record WorkerPromptResult(
    [property: JsonPropertyName("targetType")] string TargetType,
    [property: JsonPropertyName("targetId")] string TargetId,
    [property: JsonPropertyName("templateCode")] string TemplateCode,
    [property: JsonPropertyName("templateName")] string TemplateName,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("context")] WorkerPromptContextResult? Context);

public sealed record WorkerPromptContextResult(
    [property: JsonPropertyName("targetType")] string TargetType,
    [property: JsonPropertyName("targetId")] string TargetId,
    [property: JsonPropertyName("projectId")] string ProjectId,
    [property: JsonPropertyName("projectCode")] string ProjectCode,
    [property: JsonPropertyName("projectName")] string ProjectName,
    [property: JsonPropertyName("gitRepositoryId")] string? GitRepositoryId,
    [property: JsonPropertyName("gitAccountId")] string? GitAccountId,
    [property: JsonPropertyName("repositoryReference")] string? RepositoryReference,
    [property: JsonPropertyName("repositoryUrl")] string? RepositoryUrl,
    [property: JsonPropertyName("repositoryDefaultBranch")] string? RepositoryDefaultBranch,
    [property: JsonPropertyName("gitUsername")] string? GitUsername,
    [property: JsonPropertyName("gitAccessToken")] string? GitAccessToken,
    [property: JsonPropertyName("workspacePath")] string? WorkspacePath,
    [property: JsonPropertyName("requirementId")] string RequirementId,
    [property: JsonPropertyName("requirementTitle")] string RequirementTitle,
    [property: JsonPropertyName("requirementDescription")] string? RequirementDescription,
    [property: JsonPropertyName("requirementStatus")] string? RequirementStatus,
    [property: JsonPropertyName("endpointId")] string? EndpointId,
    [property: JsonPropertyName("moduleId")] string? ModuleId,
    [property: JsonPropertyName("taskId")] string? TaskId,
    [property: JsonPropertyName("taskTitle")] string? TaskTitle,
    [property: JsonPropertyName("taskDescription")] string? TaskDescription,
    [property: JsonPropertyName("bugId")] string? BugId,
    [property: JsonPropertyName("bugTitle")] string? BugTitle,
    [property: JsonPropertyName("bugDescription")] string? BugDescription,
    [property: JsonPropertyName("bugEnvironment")] string? BugEnvironment,
    [property: JsonPropertyName("bugSeverity")] string? BugSeverity,
    [property: JsonPropertyName("skillContext")] string? SkillContext,
    [property: JsonPropertyName("completionApiPath")] string CompletionApiPath,
    [property: JsonPropertyName("completionInstruction")] string CompletionInstruction);

public sealed record WorkerWorkCompletionResult(
    [property: JsonPropertyName("targetType")] string TargetType,
    [property: JsonPropertyName("targetId")] string TargetId,
    [property: JsonPropertyName("status")] string Status);

public static class WorkerPlatformStatuses
{
    public const string Idle = "idle";

    public const string Busy = "busy";

    public const string AuthRequired = "auth_required";

    public const string Error = "error";

    public const string Running = "running";

    public const string Success = "success";

    public const string CodexFailed = "codex_failed";

    public const string Blocked = "blocked";
}

public static class WorkerPlatformCommandTypes
{
    public const string Smoke = "smoke";

    public const string StartTask = "start_task";

    public const string StartBug = "start_bug";
}
