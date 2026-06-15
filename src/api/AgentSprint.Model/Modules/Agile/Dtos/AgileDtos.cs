namespace AgentSprint.Model.Modules.Agile.Dtos;

public sealed record CreateSprintProjectRequest(
    string Code,
    string Name,
    string? TestEnvironmentUrl,
    string? Description = null,
    string? FrontendTechStack = null,
    string? BackendTechStack = null,
    string? ProjectManagerId = null,
    IReadOnlyList<string>? ProductManagerIds = null,
    IReadOnlyList<string>? DeveloperIds = null,
    IReadOnlyList<string>? TesterIds = null,
    string? ArchitectId = null,
    string? TestEnvironmentId = null,
    string? GitRepositoryId = null,
    string? GitAccountId = null);

public sealed record UpdateSprintProjectRequest(
    string Name,
    string? TestEnvironmentUrl,
    string? Description = null,
    string? FrontendTechStack = null,
    string? BackendTechStack = null,
    string? ProjectManagerId = null,
    IReadOnlyList<string>? ProductManagerIds = null,
    IReadOnlyList<string>? DeveloperIds = null,
    IReadOnlyList<string>? TesterIds = null,
    string? ArchitectId = null,
    string? TestEnvironmentId = null,
    string? GitRepositoryId = null,
    string? GitAccountId = null);

public sealed record SaveGitAccountRequest(
    string Code,
    string Name,
    string Username,
    string? AccessToken = null,
    string? Description = null,
    string? Status = null);

public sealed record SaveGitRepositoryRequest(
    string Code,
    string Name,
    string RepositoryUrl,
    string? DefaultBranch = null,
    string? GitAccountId = null,
    string? LocalPath = null,
    string? Description = null,
    string? Status = null);

public sealed record CreateGitBranchRequest(
    string BranchName,
    string? SourceBranch = null);

public sealed record DeleteGitBranchRequest(
    string BranchName,
    string? BackupBranch = null);

public sealed record GitAccountResult(
    string Id,
    string Code,
    string Name,
    string Username,
    string? AccessToken,
    string? Description,
    string Status,
    string CreatedBy,
    DateTime CreateTime);

public sealed record GitRepositoryResult(
    string Id,
    string Code,
    string Name,
    string RepositoryUrl,
    string? DefaultBranch,
    string? GitAccountId,
    string? LocalPath,
    string? Description,
    string Status,
    string CreatedBy,
    DateTime CreateTime);

public sealed record GitBranchOperationResult(
    string Id,
    string RepositoryId,
    string? AccountId,
    string OperationType,
    string BranchName,
    string? SourceBranch,
    string? BackupBranch,
    string? CommitHash,
    string? CommitMessage,
    DateTime? PushedAt,
    string Status,
    string? Message,
    string CreatedBy,
    DateTime CreateTime);

public sealed record CreateSprintProjectEndpointRequest(
    string ProjectId,
    string Code,
    string Name,
    string Type,
    string? OwnerId = null,
    IReadOnlyList<string>? DeveloperIds = null,
    IReadOnlyList<string>? TesterIds = null,
    IReadOnlyList<string>? SkillIds = null,
    int? Sort = null);

public sealed record UpdateSprintProjectEndpointRequest(
    string Name,
    string Type,
    string? OwnerId = null,
    IReadOnlyList<string>? DeveloperIds = null,
    IReadOnlyList<string>? TesterIds = null,
    IReadOnlyList<string>? SkillIds = null,
    int? Sort = null,
    string? Status = null);

public sealed record CreateSprintFeatureModuleRequest(
    string ProjectId,
    string EndpointId,
    string Code,
    string Name,
    string? Description,
    string? OwnerId = null,
    IReadOnlyList<string>? DeveloperIds = null,
    IReadOnlyList<string>? TesterIds = null,
    int? Sort = null);

public sealed record UpdateSprintFeatureModuleRequest(
    string Name,
    string? Description,
    string? OwnerId = null,
    IReadOnlyList<string>? DeveloperIds = null,
    IReadOnlyList<string>? TesterIds = null,
    int? Sort = null,
    string? Status = null);

public sealed record CreateSprintRequirementRequest(
    string ProjectId,
    string Title,
    string? Description,
    int? Priority,
    string? Stakeholders = null,
    string? SourceRequirementId = null,
    string? SourceFeedbackId = null,
    string? EndpointId = null,
    string? ModuleId = null,
    IReadOnlyList<string>? SkillIds = null,
    bool RequiresReview = true);

public sealed record RejectSprintRequirementRequest(string? Reason);

public sealed record SubmitSprintRequirementReviewRequest(IReadOnlyList<string> ReviewerIds);

public sealed record DecideSprintRequirementReviewRequest(string? Comment);

public sealed record UpdateSprintRequirementRequest(
    string Title,
    string? Description,
    int? Priority,
    string? Stakeholders,
    IReadOnlyList<string>? SkillIds = null);

public sealed record CreateSprintSkillRequest(
    string? Code,
    string Name,
    string Content,
    string? Description = null,
    string? Type = null);

public sealed record UpdateSprintSkillRequest(
    string Name,
    string Content,
    string? Description = null,
    string? Status = null,
    string? Type = null);

/// <summary>
/// zh-cn: 需求拆解请求，包含补充说明、指派模式、可选任务数量，以及手动指派时选择的研发人员。
/// en-us: Requirement decomposition request containing supplemental instructions, assignment mode, optional task count, and the selected developer for manual assignment.
/// </summary>
public sealed record DecomposeSprintRequirementRequest(
    string? Instruction,
    string? AssignmentMode = null,
    int? TaskCount = null,
    string? AssigneeId = null,
    int? AssigneeType = null);

/// <summary>
/// zh-cn: 开发任务指派请求，AssigneeId 存储实际承接任务的平台账号，AssigneeType 区分员工(0)和数字员工(1)来源。
/// en-us: Development-task assignment request; AssigneeId stores the platform account that owns the work, while AssigneeType distinguishes employee (0) and digital worker (1) sources.
/// </summary>
public sealed record AssignSprintDevelopmentTaskRequest(string AssigneeId, int? AssigneeType = null);

public sealed record ClaimSprintTaskRequest(string? OwnerDevice);

public sealed record CompleteSprintDevelopmentRequest(string? TestUrl);

public sealed record CreateSprintBugRequest(
    string ProjectId,
    string RequirementId,
    string Title,
    string? Description,
    string? Environment,
    string? Severity,
    string? TestPlanId,
    string? TestExecutionId);

public sealed record CreateSprintRequirementFeedbackRequest(
    string Title,
    string? Content,
    string? DevelopmentTaskId = null);

public sealed record ConvertSprintRequirementFeedbackRequest(
    string Title,
    string? Description,
    int? Priority,
    string? Stakeholders,
    string? Remark = null);

public sealed record ConvertSprintRequirementSourcesRequest(
    string Title,
    string? Description,
    int? Priority,
    string? Stakeholders,
    IReadOnlyList<string>? FeedbackIds,
    IReadOnlyList<string>? SuggestionIds,
    string? Remark);

public sealed record CreateSprintFeatureSuggestionRequest(
    string ProjectId,
    string? EndpointId,
    string? ModuleId,
    string? RequirementId,
    string Content);

public sealed record SprintProjectResult(
    string Id,
    string Code,
    string Name,
    string? TestEnvironmentUrl,
    string? Description,
    string? FrontendTechStack,
    string? BackendTechStack,
    string? ProjectManagerId,
    IReadOnlyList<string> ProductManagerIds,
    IReadOnlyList<string> DeveloperIds,
    string? ArchitectId,
    string Status,
    string CreatedBy,
    DateTime CreateTime,
    IReadOnlyList<string>? TesterIds = null,
    string? TestEnvironmentId = null,
    string? GitRepositoryId = null,
    string? GitAccountId = null);

public sealed record SprintSkillResult(
    string Id,
    string Code,
    string Name,
    string Type,
    string? Description,
    string Content,
    string Status,
    string CreatedBy,
    DateTime CreateTime);

public sealed record SprintProjectEndpointResult(
    string Id,
    string ProjectId,
    string Code,
    string Name,
    string Type,
    string? OwnerId,
    IReadOnlyList<string> DeveloperIds,
    IReadOnlyList<string> TesterIds,
    int Sort,
    string Status,
    string CreatedBy,
    DateTime CreateTime,
    IReadOnlyList<string>? SkillIds = null);

public sealed record SprintFeatureModuleResult(
    string Id,
    string ProjectId,
    string EndpointId,
    string Code,
    string Name,
    string? Description,
    string? OwnerId,
    IReadOnlyList<string> DeveloperIds,
    IReadOnlyList<string> TesterIds,
    int Sort,
    string Status,
    string CreatedBy,
    DateTime CreateTime);

public sealed record SprintRequirementResult(
    string Id,
    string ProjectId,
    string Title,
    string? Description,
    string Status,
    int Priority,
    string CreatedBy,
    string? Stakeholders,
    string? ReviewedBy,
    string? DeveloperId,
    string? TestUrl,
    DateTime? ApprovedAt,
    DateTime? SubmittedAt,
    DateTime? DevelopmentCompletedAt,
    DateTime? TestedAt,
    DateTime? ClosedAt,
    DateTime? VoidedAt,
    string? SourceRequirementId,
    string? SourceFeedbackId,
    string Health,
    DateTime CreateTime,
    string? EndpointId = null,
    string? ModuleId = null,
    IReadOnlyList<string>? SkillIds = null);

public sealed record SprintFeatureSuggestionResult(
    string Id,
    string ProjectId,
    string? EndpointId,
    string? ModuleId,
    string? RequirementId,
    string Content,
    string Status,
    string CreatedBy,
    string? ConvertedRequirementId,
    DateTime? ConvertedAt,
    DateTime CreateTime);

public sealed record SprintRequirementReviewResult(
    string Id,
    string ProjectId,
    string RequirementId,
    string ReviewerId,
    string Status,
    string? Comment,
    DateTime? ReviewedAt,
    DateTime CreateTime);

public sealed record SprintRequirementReviewItemResult(
    SprintRequirementResult Requirement,
    SprintProjectResult Project,
    IReadOnlyList<SprintRequirementReviewResult> Reviews);

public sealed record SprintDevelopmentTaskResult(
    string Id,
    string ProjectId,
    string RequirementId,
    string? EndpointId,
    string? ModuleId,
    string Title,
    string? Description,
    string Status,
    int Priority,
    string? AssigneeId,
    int AssigneeType,
    string? AssignedBy,
    string CreatedBy,
    string? Prompt,
    DateTime? AssignedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? UpdateTime,
    DateTime CreateTime);

public sealed record SprintTaskPromptSectionResult(
    string Title,
    string Content,
    IReadOnlyList<string> Usage,
    IReadOnlyList<string> Notes);

public sealed record SprintTaskPromptResult(
    string TaskId,
    string Prompt,
    SprintTaskPromptSectionResult McpSetupPrompt,
    SprintTaskPromptSectionResult TaskExecutionPrompt);

public sealed record SprintBugResult(
    string Id,
    string ProjectId,
    string RequirementId,
    string? TestPlanId,
    string? TestExecutionId,
    string Title,
    string? Description,
    string Environment,
    string Severity,
    string Status,
    string CreatedBy,
    string? DeveloperId,
    DateTime? FixedAt,
    DateTime CreateTime);

public sealed record SprintRequirementFeedbackResult(
    string Id,
    string ProjectId,
    string RequirementId,
    string? DevelopmentTaskId,
    string Title,
    string? Content,
    string Status,
    string CreatedBy,
    string? ConvertedRequirementId,
    DateTime? ConvertedAt,
    DateTime? ClosedAt,
    DateTime CreateTime);

public sealed record SprintTaskLeaseResult(
    string Id,
    string ProjectId,
    string TargetType,
    string TargetId,
    string OwnerId,
    string? OwnerDevice,
    string LeaseToken,
    string Status,
    DateTime ExpiresAt,
    DateTime? CompletedAt,
    DateTime CreateTime);

public sealed record SprintMvpSummaryResult(
    int ProjectCount,
    int RequirementCount,
    int ReadyRequirementCount,
    int DevelopingRequirementCount,
    int ReadyTestRequirementCount,
    int TestingRequirementCount,
    int CompletedRequirementCount,
    int OpenBugCount,
    int ActiveLeaseCount);
