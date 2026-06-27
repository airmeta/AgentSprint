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
    [property: JsonPropertyName("resultJson")] string? ResultJson,
    [property: JsonPropertyName("changedFilesJson")] string? ChangedFilesJson = null,
    [property: JsonPropertyName("gitCommitId")] string? GitCommitId = null);

public sealed record ReportWorkerEventRequest(
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("runId")] string? RunId,
    [property: JsonPropertyName("level")] string? Level,
    [property: JsonPropertyName("payloadJson")] string? PayloadJson);

public sealed record AppendWorkerCommandLogRequest(
    [property: JsonPropertyName("commandId")] string CommandId,
    [property: JsonPropertyName("chunk")] string? Chunk,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("runId")] string? RunId,
    [property: JsonPropertyName("instanceId")] string? InstanceId,
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("completed")] bool Completed,
    [property: JsonPropertyName("startedAt")] DateTime? StartedAt,
    [property: JsonPropertyName("completedAt")] DateTime? CompletedAt);

public sealed record WorkerSessionResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("instanceId")] string InstanceId,
    [property: JsonPropertyName("status")] string Status);

public sealed record WorkerCommandResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("commandType")] string CommandType,
    [property: JsonPropertyName("title")] string Title,
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
    [property: JsonPropertyName("openAiApiKey")] string? OpenAiApiKey,
    [property: JsonPropertyName("agentToken")] string? AgentToken,
    [property: JsonPropertyName("configVersion")] int ConfigVersion);

public sealed record StartupProbeConfigResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("expectedPattern")] string? ExpectedPattern,
    [property: JsonPropertyName("required")] bool Required,
    [property: JsonPropertyName("sort")] int Sort);

public sealed record StartupProbeResultReportItem(
    [property: JsonPropertyName("probeConfigId")] string ProbeConfigId,
    [property: JsonPropertyName("probeCode")] string ProbeCode,
    [property: JsonPropertyName("probeName")] string ProbeName,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("exitCode")] int? ExitCode,
    [property: JsonPropertyName("stdout")] string? Stdout,
    [property: JsonPropertyName("stderr")] string? Stderr,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("passed")] bool Passed,
    [property: JsonPropertyName("required")] bool Required);

public sealed record ReportStartupProbeResultsRequest(
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("instanceId")] string InstanceId,
    [property: JsonPropertyName("workerDeployRenderId")] string? WorkerDeployRenderId,
    [property: JsonPropertyName("results")] IReadOnlyList<StartupProbeResultReportItem> Results);

public sealed record StartupProbeResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("instanceId")] string InstanceId,
    [property: JsonPropertyName("workerDeployRenderId")] string? WorkerDeployRenderId,
    [property: JsonPropertyName("probeConfigId")] string ProbeConfigId,
    [property: JsonPropertyName("probeCode")] string ProbeCode,
    [property: JsonPropertyName("probeName")] string ProbeName,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("exitCode")] int? ExitCode,
    [property: JsonPropertyName("passed")] bool Passed,
    [property: JsonPropertyName("required")] bool Required);

public sealed record WorkerRunResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("status")] string Status);

public sealed record WorkerEventResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("eventType")] string EventType);

public sealed record WorkerCommandLogSnapshotResult(
    [property: JsonPropertyName("commandId")] string CommandId,
    [property: JsonPropertyName("runId")] string? RunId,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("instanceId")] string InstanceId,
    [property: JsonPropertyName("logText")] string LogText,
    [property: JsonPropertyName("lastSequence")] long LastSequence,
    [property: JsonPropertyName("completed")] bool Completed,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt);

public sealed record CodeAuditTaskDetailResult(
    [property: JsonPropertyName("task")] CodeAuditTaskResult Task,
    [property: JsonPropertyName("result")] CodeAuditResultResult? Result);

public sealed record CodeAuditTaskResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("projectId")] string ProjectId,
    [property: JsonPropertyName("gitRepositoryId")] string GitRepositoryId,
    [property: JsonPropertyName("branch")] string Branch,
    [property: JsonPropertyName("workerId")] string WorkerId,
    [property: JsonPropertyName("auditTargetType")] string AuditTargetType,
    [property: JsonPropertyName("targetId")] string? TargetId,
    [property: JsonPropertyName("sourceTaskId")] string? SourceTaskId,
    [property: JsonPropertyName("sourceCommandId")] string? SourceCommandId,
    [property: JsonPropertyName("auditCommandId")] string? AuditCommandId,
    [property: JsonPropertyName("sourceRunId")] string? SourceRunId,
    [property: JsonPropertyName("baseCommitId")] string? BaseCommitId,
    [property: JsonPropertyName("headCommitId")] string? HeadCommitId,
    [property: JsonPropertyName("currentBranchHeadCommitId")] string? CurrentBranchHeadCommitId,
    [property: JsonPropertyName("sourceGitCommitId")] string? SourceGitCommitId,
    [property: JsonPropertyName("requirementId")] string? RequirementId,
    [property: JsonPropertyName("moduleId")] string? ModuleId,
    [property: JsonPropertyName("scopeJson")] string? ScopeJson,
    [property: JsonPropertyName("selectedSkillIds")] IReadOnlyList<string> SelectedSkillIds,
    [property: JsonPropertyName("instruction")] string? Instruction,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("conclusion")] string? Conclusion,
    [property: JsonPropertyName("workspaceDirtyReason")] string? WorkspaceDirtyReason);

public sealed record CodeAuditResultResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("auditTaskId")] string AuditTaskId,
    [property: JsonPropertyName("workerCommandId")] string? WorkerCommandId,
    [property: JsonPropertyName("workerRunId")] string? WorkerRunId,
    [property: JsonPropertyName("rawResult")] string? RawResult);

public sealed record CodeAuditExecutionContextResult(
    [property: JsonPropertyName("task")] CodeAuditTaskResult Task,
    [property: JsonPropertyName("projectCode")] string ProjectCode,
    [property: JsonPropertyName("projectName")] string ProjectName,
    [property: JsonPropertyName("repositoryUrl")] string RepositoryUrl,
    [property: JsonPropertyName("repositoryReference")] string? RepositoryReference,
    [property: JsonPropertyName("repositoryDefaultBranch")] string? RepositoryDefaultBranch,
    [property: JsonPropertyName("gitUsername")] string? GitUsername,
    [property: JsonPropertyName("gitAccessToken")] string? GitAccessToken,
    [property: JsonPropertyName("gitCommitAuthorName")] string? GitCommitAuthorName,
    [property: JsonPropertyName("gitCommitAuthorEmail")] string? GitCommitAuthorEmail,
    [property: JsonPropertyName("templateCode")] string TemplateCode,
    [property: JsonPropertyName("templateName")] string TemplateName,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("promptSnapshot")] string PromptSnapshot,
    [property: JsonPropertyName("skillContextSnapshot")] string SkillContextSnapshot,
    [property: JsonPropertyName("sourceCommitId")] string? SourceCommitId,
    [property: JsonPropertyName("sourceRunId")] string? SourceRunId,
    [property: JsonPropertyName("sourceCommandId")] string? SourceCommandId,
    [property: JsonPropertyName("changedFilesJson")] string? ChangedFilesJson,
    [property: JsonPropertyName("diff")] string? Diff,
    [property: JsonPropertyName("codeContext")] string? CodeContext,
    [property: JsonPropertyName("gitContextWarnings")] string? GitContextWarnings,
    [property: JsonPropertyName("scopeDescription")] string? ScopeDescription,
    [property: JsonPropertyName("targetSummary")] string? TargetSummary);

public sealed record PrepareCodeAuditContextRequest(
    [property: JsonPropertyName("workerRunId")] string? WorkerRunId,
    [property: JsonPropertyName("branch")] string? Branch,
    [property: JsonPropertyName("baseCommitId")] string? BaseCommitId,
    [property: JsonPropertyName("headCommitId")] string? HeadCommitId,
    [property: JsonPropertyName("currentBranchHeadCommitId")] string? CurrentBranchHeadCommitId,
    [property: JsonPropertyName("changedFilesJson")] string? ChangedFilesJson,
    [property: JsonPropertyName("diff")] string? Diff,
    [property: JsonPropertyName("codeContext")] string? CodeContext,
    [property: JsonPropertyName("sourceCommitReachable")] bool? SourceCommitReachable,
    [property: JsonPropertyName("sourceCommitBehindHead")] bool? SourceCommitBehindHead,
    [property: JsonPropertyName("warning")] string? Warning);

public sealed record SyncCodeAuditFileIndexRequest(
    [property: JsonPropertyName("projectId")] string ProjectId,
    [property: JsonPropertyName("gitRepositoryId")] string GitRepositoryId,
    [property: JsonPropertyName("branch")] string Branch,
    [property: JsonPropertyName("commitId")] string? CommitId,
    [property: JsonPropertyName("files")] IReadOnlyList<CodeAuditFileIndexItem> Files);

public sealed record CodeAuditFileIndexItem(
    [property: JsonPropertyName("filePath")] string FilePath,
    [property: JsonPropertyName("fileContentHash")] string? FileContentHash,
    [property: JsonPropertyName("fileType")] string? FileType);

public sealed record CodeAuditFileIndexSyncResult(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("created")] int Created,
    [property: JsonPropertyName("updated")] int Updated,
    [property: JsonPropertyName("deleted")] int Deleted);

public sealed record CompleteCodeAuditTaskRequest(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("conclusion")] string? Conclusion,
    [property: JsonPropertyName("workerRunId")] string? WorkerRunId,
    [property: JsonPropertyName("gitCommitId")] string? GitCommitId,
    [property: JsonPropertyName("branch")] string? Branch,
    [property: JsonPropertyName("changedFilesJson")] string? ChangedFilesJson,
    [property: JsonPropertyName("promptSnapshot")] string? PromptSnapshot,
    [property: JsonPropertyName("skillContextSnapshot")] string? SkillContextSnapshot,
    [property: JsonPropertyName("rawResult")] string? RawResult,
    [property: JsonPropertyName("structuredResultJson")] string? StructuredResultJson,
    [property: JsonPropertyName("issuesJson")] string? IssuesJson,
    [property: JsonPropertyName("annotationIssuesJson")] string? AnnotationIssuesJson,
    [property: JsonPropertyName("manualCheckItemsJson")] string? ManualCheckItemsJson,
    [property: JsonPropertyName("workspaceDirtyReason")] string? WorkspaceDirtyReason);

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
    [property: JsonPropertyName("gitCommitAuthorName")] string? GitCommitAuthorName,
    [property: JsonPropertyName("gitCommitAuthorEmail")] string? GitCommitAuthorEmail,
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

    public const string Cancelled = "cancelled";
}

public static class WorkerPlatformCommandTypes
{
    public const string Smoke = "smoke";

    public const string StartTask = "start_task";

    public const string StartBug = "start_bug";

    public const string CancelCurrentRun = "cancel_current_run";

    public const string StopAfterCurrent = "stop_after_current";

    public const string ReloadConfig = "reload_config";

    public const string CodeAudit = "code_audit";

    public const string CodeAuditIndexSync = "code_audit_index_sync";
}
