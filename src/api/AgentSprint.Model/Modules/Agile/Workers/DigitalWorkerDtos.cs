namespace AgentSprint.Model.Modules.Agile.Workers;

public sealed record CreateDigitalWorkerRequest(
    string Name,
    string AgentUserId,
    string? Code = null,
    string? AgentTokenId = null,
    IReadOnlyList<string>? ProjectIds = null,
    IReadOnlyList<string>? EndpointIds = null,
    IReadOnlyList<string>? SkillIds = null,
    string? EmployeeType = null,
    string? WorkerType = null,
    int? MaxConcurrentRuns = null,
    int? HeartbeatTimeoutSeconds = null,
    int? PollIntervalSeconds = null,
    int? IdleMaxIntervalSeconds = null,
    int? MaxRunMinutes = null,
    string? WorkspaceRoot = null,
    string? RunsRoot = null,
    string? CodexHome = null,
    string? SandboxMode = null,
    bool? RunSmokeOnStartup = null,
    string? SmokePrompt = null,
    string? CodexProvider = null,
    string? CodexModel = null,
    string? OpenAiBaseUrl = null,
    string? Description = null);

public sealed record UpdateDigitalWorkerRequest(
    string Name,
    string AgentUserId,
    string? AgentTokenId = null,
    IReadOnlyList<string>? ProjectIds = null,
    IReadOnlyList<string>? EndpointIds = null,
    IReadOnlyList<string>? SkillIds = null,
    string? EmployeeType = null,
    string? WorkerType = null,
    string? Status = null,
    int? MaxConcurrentRuns = null,
    int? HeartbeatTimeoutSeconds = null,
    int? PollIntervalSeconds = null,
    int? IdleMaxIntervalSeconds = null,
    int? MaxRunMinutes = null,
    string? WorkspaceRoot = null,
    string? RunsRoot = null,
    string? CodexHome = null,
    string? SandboxMode = null,
    bool? RunSmokeOnStartup = null,
    string? SmokePrompt = null,
    string? CodexProvider = null,
    string? CodexModel = null,
    string? OpenAiBaseUrl = null,
    string? Description = null);

public sealed record SetDigitalWorkerStatusRequest(string Status);

public sealed record CreateWorkerCommandRequest(
    string WorkerId,
    string CommandType,
    string? PayloadJson = null,
    string? SessionId = null,
    DateTime? ExpiresAt = null);

public sealed record RegisterWorkerSessionRequest(
    string WorkerId,
    string InstanceId,
    string? HostName = null,
    string? ContainerId = null,
    string? CodexVersion = null,
    string? GitVersion = null,
    string? DotnetVersion = null,
    string? NodeVersion = null,
    bool ConfigTomlExists = false,
    string? CodexHome = null,
    string? WorkspaceRoot = null,
    string? RunsRoot = null,
    string? ErrorSummary = null);

public sealed record WorkerHeartbeatRequest(
    string WorkerId,
    string SessionId,
    string Status,
    string? CurrentRunId = null,
    string? ErrorSummary = null);

public sealed record AckWorkerCommandRequest(string SessionId);

public sealed record StartWorkerRunRequest(
    string WorkerId,
    string SessionId,
    string RunType,
    string Status,
    string? CommandId = null,
    string? TargetType = null,
    string? TargetId = null,
    string? WorkspacePath = null,
    string? PromptPath = null,
    string? StdoutPath = null,
    string? StderrPath = null,
    string? FinalPath = null,
    string? ManifestPath = null);

public sealed record FinishWorkerRunRequest(
    string Status,
    int? ExitCode = null,
    bool TimedOut = false,
    string? Error = null,
    string? ResultJson = null);

public sealed record ReportWorkerEventRequest(
    string WorkerId,
    string EventType,
    string Message,
    string? SessionId = null,
    string? RunId = null,
    string? Level = null,
    string? PayloadJson = null);

public sealed record DigitalWorkerResult(
    string Id,
    string Code,
    string Name,
    string AgentUserId,
    string? AgentTokenId,
    IReadOnlyList<string> ProjectIds,
    IReadOnlyList<string> EndpointIds,
    IReadOnlyList<string> SkillIds,
    string EmployeeType,
    string WorkerType,
    string Status,
    int MaxConcurrentRuns,
    int HeartbeatTimeoutSeconds,
    int PollIntervalSeconds,
    int IdleMaxIntervalSeconds,
    int MaxRunMinutes,
    string WorkspaceRoot,
    string RunsRoot,
    string CodexHome,
    string SandboxMode,
    bool RunSmokeOnStartup,
    string? SmokePrompt,
    string CodexProvider,
    string CodexModel,
    string? OpenAiBaseUrl,
    int ConfigVersion,
    string? Description,
    string CreatedBy,
    DateTime CreateTime,
    DateTime? UpdateTime);

public sealed record WorkerRuntimeConfigResult(
    string WorkerId,
    string WorkerCode,
    string WorkerName,
    string? ProjectId,
    string? ProjectCode,
    string WorkspaceRoot,
    string RunsRoot,
    string CodexHome,
    int PollIntervalSeconds,
    int IdleMaxIntervalSeconds,
    int MaxRunMinutes,
    string SandboxMode,
    bool RunSmokeOnStartup,
    string SmokePrompt,
    string CodexProvider,
    string CodexModel,
    string? OpenAiBaseUrl,
    string? AgentToken,
    int ConfigVersion);

public sealed record WorkerSessionResult(
    string Id,
    string WorkerId,
    string InstanceId,
    string? HostName,
    string? ContainerId,
    string Status,
    string? CodexVersion,
    string? GitVersion,
    string? DotnetVersion,
    string? NodeVersion,
    bool ConfigTomlExists,
    string? CodexHome,
    string? WorkspaceRoot,
    string? RunsRoot,
    string? CurrentRunId,
    string? ErrorSummary,
    DateTime? LastHeartbeatAt,
    DateTime StartedAt,
    DateTime? StoppedAt);

public sealed record WorkerCommandResult(
    string Id,
    string WorkerId,
    string? SessionId,
    string CommandType,
    string? PayloadJson,
    string Status,
    DateTime? AckedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? ExpiresAt,
    string? ResultJson,
    string? Error,
    string CreatedBy,
    DateTime CreateTime);

public sealed record WorkerRunResult(
    string Id,
    string WorkerId,
    string SessionId,
    string? CommandId,
    string RunType,
    string? TargetType,
    string? TargetId,
    string Status,
    string? WorkspacePath,
    string? PromptPath,
    string? StdoutPath,
    string? StderrPath,
    string? FinalPath,
    string? ManifestPath,
    int? ExitCode,
    bool TimedOut,
    string? Error,
    DateTime StartedAt,
    DateTime? CompletedAt);

public sealed record WorkerEventResult(
    string Id,
    string WorkerId,
    string? SessionId,
    string? RunId,
    string EventType,
    string Level,
    string Message,
    string? PayloadJson,
    DateTime CreateTime);

public sealed record WorkerHeartbeatResult(
    string WorkerId,
    string SessionId,
    string Status,
    int NextIntervalSeconds,
    IReadOnlyList<WorkerCommandResult> Commands);

public sealed record DigitalWorkerDetailResult(
    DigitalWorkerResult Worker,
    WorkerSessionResult? LatestSession,
    WorkerRunResult? CurrentRun,
    IReadOnlyList<WorkerCommandResult> PendingCommands);

public sealed record WorkerPromptContextResult(
    string TargetType,
    string TargetId,
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    string? GitRepositoryId,
    string? GitAccountId,
    string? RepositoryReference,
    string? RepositoryUrl,
    string? RepositoryDefaultBranch,
    string? GitUsername,
    string? GitAccessToken,
    string? WorkspacePath,
    string RequirementId,
    string RequirementTitle,
    string? RequirementDescription,
    string? RequirementStatus,
    string? EndpointId,
    string? ModuleId,
    string? TaskId,
    string? TaskTitle,
    string? TaskDescription,
    string? BugId,
    string? BugTitle,
    string? BugDescription,
    string? BugEnvironment,
    string? BugSeverity,
    string? SkillContext,
    string CompletionApiPath,
    string CompletionInstruction);

public sealed record WorkerPromptResult(
    string TargetType,
    string TargetId,
    string TemplateCode,
    string TemplateName,
    string Prompt,
    WorkerPromptContextResult Context);

public sealed record WorkerWorkCompletionResult(
    string TargetType,
    string TargetId,
    string Status);
