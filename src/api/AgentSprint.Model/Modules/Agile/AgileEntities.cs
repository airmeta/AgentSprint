using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using AgentSprint.Model.Modules.Common;

namespace AgentSprint.Model.Modules.Agile;

[Table("sprint_project")]
public sealed class SprintProjectEntity : EntityBase
{
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? GitRepositoryId { get; set; }

    [MaxLength(64)]
    public string? GitAccountId { get; set; }

    [MaxLength(64)]
    public string AiPlatformCode { get; set; } = "openai";

    [MaxLength(512)]
    public string? TestEnvironmentUrl { get; set; }

    [MaxLength(64)]
    public string? TestEnvironmentId { get; set; }

    [MaxLength(2048)]
    public string? Description { get; set; }

    [MaxLength(512)]
    public string? FrontendTechStack { get; set; }

    [MaxLength(512)]
    public string? BackendTechStack { get; set; }

    [MaxLength(64)]
    public string? ProjectManagerId { get; set; }

    [MaxLength(512)]
    public string? ProductManagerIds { get; set; }

    [MaxLength(1024)]
    public string? DeveloperIds { get; set; }

    [MaxLength(1024)]
    public string? TesterIds { get; set; }

    [MaxLength(64)]
    public string? ArchitectId { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = SprintProjectStatuses.Active;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("git_account")]
public sealed class GitAccountEntity : EntityBase
{
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? AccessToken { get; set; }

    [MaxLength(128)]
    public string? CommitAuthorName { get; set; }

    [MaxLength(256)]
    public string? CommitAuthorEmail { get; set; }

    [MaxLength(512)]
    public string? Description { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = GitAccountStatuses.Active;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("git_repository")]
public sealed class GitRepositoryEntity : EntityBase
{
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(512)]
    public string RepositoryUrl { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? DefaultBranch { get; set; }

    [MaxLength(64)]
    public string? GitAccountId { get; set; }

    [MaxLength(512)]
    public string? LocalPath { get; set; }

    [MaxLength(512)]
    public string? Description { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = GitRepositoryStatuses.Active;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("git_branch_operation")]
public sealed class GitBranchOperationEntity : EntityBase
{
    [MaxLength(64)]
    public string RepositoryId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? AccountId { get; set; }

    [MaxLength(32)]
    public string OperationType { get; set; } = GitBranchOperationTypes.PushRecord;

    [MaxLength(128)]
    public string BranchName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? SourceBranch { get; set; }

    [MaxLength(128)]
    public string? BackupBranch { get; set; }

    [MaxLength(64)]
    public string? CommitHash { get; set; }

    [MaxLength(512)]
    public string? CommitMessage { get; set; }

    public DateTime? PushedAt { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = GitBranchOperationStatuses.Success;

    [MaxLength(2048)]
    public string? Message { get; set; }

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("sprint_project_member")]
public sealed class SprintProjectMemberEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Role { get; set; } = SprintProjectMemberRoles.Developer;

    [MaxLength(32)]
    public string Status { get; set; } = SprintProjectMemberStatuses.Active;
}

[Table("sprint_project_material")]
public sealed class SprintProjectMaterialEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? ParentId { get; set; }

    [MaxLength(16)]
    public string ItemType { get; set; } = SprintProjectMaterialItemTypes.File;

    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? OriginalFileName { get; set; }

    [MaxLength(32)]
    public string? Extension { get; set; }

    [MaxLength(128)]
    public string? ContentType { get; set; }

    public long SizeBytes { get; set; }

    [MaxLength(64)]
    public string StorageRoot { get; set; } = SprintProjectMaterialStorageRoots.ApiRunDirectory;

    [MaxLength(1024)]
    public string? RelativePath { get; set; }

    [MaxLength(128)]
    public string? Sha256 { get; set; }

    [MaxLength(64)]
    public string? Category { get; set; }

    [MaxLength(1024)]
    public string? TagsJson { get; set; }

    [MaxLength(2048)]
    public string? Description { get; set; }

    [MaxLength(32)]
    public string ExtractStatus { get; set; } = SprintProjectMaterialExtractStatuses.None;

    [MaxLength(1024)]
    public string? ExtractedTextPath { get; set; }

    [Column(TypeName = "text")]
    public string? Summary { get; set; }

    [MaxLength(64)]
    public string UploadedBy { get; set; } = string.Empty;

    public DateTime? DeletedAt { get; set; }
}

[Table("sprint_project_material_event")]
public sealed class SprintProjectMaterialEventEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string MaterialId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string EventType { get; set; } = string.Empty;

    [Column(TypeName = "text")]
    public string? PayloadJson { get; set; }

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("sprint_proposal")]
public sealed class SprintProposalEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = SprintProposalStatuses.Draft;

    [MaxLength(32)]
    public string SourceType { get; set; } = SprintProposalSourceTypes.Manual;

    [MaxLength(2048)]
    public string? Instruction { get; set; }

    [Column(TypeName = "text")]
    public string? Content { get; set; }

    [MaxLength(2048)]
    public string? Summary { get; set; }

    [Column(TypeName = "text")]
    public string? AiPromptSnapshot { get; set; }

    [Column(TypeName = "text")]
    public string? AiResultSnapshot { get; set; }

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? ConvertedAt { get; set; }

    public DateTime? VoidedAt { get; set; }
}

[Table("sprint_proposal_material")]
public sealed class SprintProposalMaterialEntity : EntityBase
{
    [MaxLength(64)]
    public string ProposalId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string MaterialId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? MaterialVersionHash { get; set; }

    [MaxLength(1024)]
    public string? ExtractedTextSnapshotPath { get; set; }
}

[Table("sprint_proposal_conversation")]
public sealed class SprintProposalConversationEntity : EntityBase
{
    [MaxLength(64)]
    public string ProposalId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(16)]
    public string Role { get; set; } = SprintProposalConversationRoles.User;

    [Column(TypeName = "text")]
    public string Content { get; set; } = string.Empty;

    [Column(TypeName = "text")]
    public string? MaterialIdsJson { get; set; }

    [Column(TypeName = "text")]
    public string? TokenUsageJson { get; set; }

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("sprint_proposal_requirement")]
public sealed class SprintProposalRequirementEntity : EntityBase
{
    [MaxLength(64)]
    public string ProposalId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RequirementId { get; set; } = string.Empty;

    [Column(TypeName = "text")]
    public string? MaterialIdsJson { get; set; }

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("sprint_project_endpoint")]
public sealed class SprintProjectEndpointEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Type { get; set; } = SprintProjectEndpointTypes.Other;

    [MaxLength(64)]
    public string? OwnerId { get; set; }

    [MaxLength(1024)]
    public string? DeveloperIds { get; set; }

    [MaxLength(1024)]
    public string? TesterIds { get; set; }

    [MaxLength(1024)]
    public string? SkillIds { get; set; }

    public int Sort { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = SprintProjectEndpointStatuses.Active;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("sprint_feature_module")]
public sealed class SprintFeatureModuleEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string EndpointId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [MaxLength(64)]
    public string? OwnerId { get; set; }

    [MaxLength(1024)]
    public string? DeveloperIds { get; set; }

    [MaxLength(1024)]
    public string? TesterIds { get; set; }

    public int Sort { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = SprintFeatureModuleStatuses.Active;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("sprint_requirement")]
public sealed class SprintRequirementEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string EndpointId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ModuleId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? Description { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = SprintRequirementStatuses.Draft;

    public int Priority { get; set; } = 3;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Stakeholders { get; set; }

    [MaxLength(64)]
    public string? ReviewedBy { get; set; }

    [MaxLength(64)]
    public string? DeveloperId { get; set; }

    [MaxLength(512)]
    public string? TestUrl { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? DevelopmentCompletedAt { get; set; }

    public DateTime? TestedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? VoidedAt { get; set; }

    [MaxLength(64)]
    public string? SourceRequirementId { get; set; }

    [MaxLength(64)]
    public string? SourceFeedbackId { get; set; }

    [MaxLength(1024)]
    public string? SkillIds { get; set; }
}

[Table("sprint_skill")]
public sealed class SprintSkillEntity : EntityBase
{
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Type { get; set; } = SprintSkillTypes.Development;

    [MaxLength(512)]
    public string? Description { get; set; }

    [MaxLength(8192)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = SprintSkillStatuses.Active;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("sprint_feature_suggestion")]
public sealed class SprintFeatureSuggestionEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? EndpointId { get; set; }

    [MaxLength(64)]
    public string? ModuleId { get; set; }

    [MaxLength(64)]
    public string? RequirementId { get; set; }

    [MaxLength(2048)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = SprintFeatureSuggestionStatuses.Open;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? ConvertedRequirementId { get; set; }

    public DateTime? ConvertedAt { get; set; }
}

[Table("sprint_requirement_feedback")]
public sealed class SprintRequirementFeedbackEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RequirementId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? DevelopmentTaskId { get; set; }

    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? Content { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = SprintRequirementFeedbackStatuses.Open;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? ConvertedRequirementId { get; set; }

    public DateTime? ConvertedAt { get; set; }

    public DateTime? ClosedAt { get; set; }
}

[Table("sprint_requirement_review")]
public sealed class SprintRequirementReviewEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RequirementId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ReviewerId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = SprintRequirementReviewStatuses.Pending;

    [MaxLength(512)]
    public string? Comment { get; set; }

    [MaxLength(1024)]
    public string? SubmitReason { get; set; }

    public DateTime? ReviewedAt { get; set; }
}

[Table("sprint_development_task")]
public sealed class SprintDevelopmentTaskEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RequirementId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? Description { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = SprintDevelopmentTaskStatuses.PendingAssign;

    public int Priority { get; set; } = 3;

    [MaxLength(64)]
    public string? AssigneeId { get; set; }

    public int AssigneeType { get; set; } = SprintTaskAssigneeTypes.Employee;

    [MaxLength(64)]
    public string? AssignedBy { get; set; }

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(8192)]
    public string? Prompt { get; set; }

    public DateTime? AssignedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

[Table("sprint_requirement_decomposition_preview")]
public sealed class SprintRequirementDecompositionPreviewEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RequirementId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Source { get; set; } = "local";

    [MaxLength(32)]
    public string Status { get; set; } = SprintRequirementDecompositionPreviewStatuses.Draft;

    [Column(TypeName = "text")]
    public string TaskJson { get; set; } = "[]";

    [Column(TypeName = "text")]
    public string? RawContent { get; set; }

    [Column(TypeName = "text")]
    public string? Instruction { get; set; }

    [MaxLength(64)]
    public string? AiPlatformCode { get; set; }

    [Column(TypeName = "text")]
    public string? ErrorMessage { get; set; }

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }
}

public static class SprintRequirementDecompositionPreviewStatuses
{
    public const string Draft = "draft";

    public const string Confirmed = "confirmed";
}

[Table("sprint_bug")]
public sealed class SprintBugEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RequirementId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? TestPlanId { get; set; }

    [MaxLength(64)]
    public string? TestExecutionId { get; set; }

    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? Description { get; set; }

    [MaxLength(32)]
    public string Environment { get; set; } = "test";

    [MaxLength(32)]
    public string Severity { get; set; } = SprintBugSeverities.Major;

    [MaxLength(32)]
    public string Status { get; set; } = SprintBugStatuses.Open;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? DeveloperId { get; set; }

    public DateTime? FixedAt { get; set; }
}

[Table("sprint_task_lease")]
public sealed class SprintTaskLeaseEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string TargetType { get; set; } = SprintTaskTargetTypes.Requirement;

    [MaxLength(64)]
    public string TargetId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? ActiveTargetKey { get; set; }

    [MaxLength(64)]
    public string OwnerId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? OwnerDevice { get; set; }

    [MaxLength(64)]
    public string LeaseToken { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(32)]
    public string Status { get; set; } = SprintTaskLeaseStatuses.Active;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(8);

    public DateTime? CompletedAt { get; set; }
}

public static class SprintProjectStatuses
{
    public const string Active = "active";

    public const string Archived = "archived";
}

public static class GitAccountStatuses
{
    public const string Active = "active";

    public const string Disabled = "disabled";
}

public static class GitRepositoryStatuses
{
    public const string Active = "active";

    public const string Disabled = "disabled";
}

public static class GitBranchOperationTypes
{
    public const string CreateBranch = "create_branch";

    public const string DeleteBranch = "delete_branch";

    public const string PushRecord = "push_record";
}

public static class GitBranchOperationStatuses
{
    public const string Success = "success";

    public const string Failed = "failed";
}

public static class SprintProjectMemberRoles
{
    public const string ProjectManager = "project_manager";

    public const string Product = "product";

    public const string Architect = "architect";

    public const string Reviewer = "reviewer";

    public const string Developer = "developer";

    public const string Tester = "tester";
}

public static class SprintProjectMemberStatuses
{
    public const string Active = "active";

    public const string Disabled = "disabled";
}

public static class SprintProjectMaterialItemTypes
{
    public const string File = "file";

    public const string Folder = "folder";
}

public static class SprintProjectMaterialStorageRoots
{
    public const string ApiRunDirectory = "api_run_directory";
}

public static class SprintProjectMaterialExtractStatuses
{
    public const string None = "none";

    public const string Pending = "pending";

    public const string Completed = "completed";

    public const string Failed = "failed";

    public const string Unsupported = "unsupported";
}

public static class SprintProjectMaterialEventTypes
{
    public const string Uploaded = "project_material_uploaded";

    public const string FolderCreated = "project_material_folder_created";

    public const string Renamed = "project_material_renamed";

    public const string Moved = "project_material_moved";

    public const string Deleted = "project_material_deleted";

    public const string Downloaded = "project_material_downloaded";

    public const string ExtractCompleted = "project_material_extract_completed";

    public const string ExtractFailed = "project_material_extract_failed";
}

public static class SprintProposalStatuses
{
    public const string Draft = "draft";

    public const string Generating = "generating";

    public const string Generated = "generated";

    public const string Confirmed = "confirmed";

    public const string Converted = "converted";

    public const string Voided = "voided";
}

public static class SprintProposalSourceTypes
{
    public const string Manual = "manual";

    public const string ProjectMaterials = "project_materials";

    public const string AiChat = "ai_chat";
}

public static class SprintProposalConversationRoles
{
    public const string System = "system";

    public const string User = "user";

    public const string Assistant = "assistant";
}

public static class SprintProjectEndpointTypes
{
    public const string Ios = "ios";

    public const string Android = "android";

    public const string Desktop = "desktop";

    public const string Web = "web";

    public const string Admin = "admin";

    public const string Other = "other";
}

public static class SprintProjectEndpointStatuses
{
    public const string Active = "active";

    public const string Disabled = "disabled";
}

public static class SprintFeatureModuleStatuses
{
    public const string Active = "active";

    public const string Disabled = "disabled";
}

public static class SprintFeatureSuggestionStatuses
{
    public const string Open = "open";

    public const string Accepted = "accepted";

    public const string Closed = "closed";
}

public static class SprintRequirementStatuses
{
    public const string Draft = "draft";

    public const string PendingReview = "pending_review";

    public const string Rejected = "rejected";

    public const string Approved = "approved";

    public const string AiDecomposing = "ai_decomposing";

    public const string AiDecomposed = "ai_decomposed";

    public const string Decomposed = "decomposed";

    public const string ReadyForDevelopment = "ready_development";

    public const string Developing = "developing";

    public const string ReadyForTest = "ready_test";

    public const string Testing = "testing";

    public const string TestFailed = "test_failed";

    public const string PendingFix = "pending_fix";

    public const string Tested = "tested";

    public const string Completed = "completed";

    public const string Voided = "voided";
}

public static class SprintSkillStatuses
{
    public const string Active = "active";

    public const string Disabled = "disabled";
}

public static class SprintSkillTypes
{
    public const string Development = "development";

    public const string Debugging = "debugging";

    public const string Operations = "operations";

    public const string RequirementAnalysis = "requirement_analysis";

    public const string Other = "other";
}

public static class SprintRequirementReviewStatuses
{
    public const string Pending = "pending";

    public const string Approved = "approved";

    public const string Rejected = "rejected";
}

public static class SprintRequirementFeedbackStatuses
{
    public const string Open = "open";

    public const string Converted = "converted";

    public const string Closed = "closed";
}

public static class SprintDevelopmentTaskStatuses
{
    public const string PendingAssign = "pending_assign";

    public const string Assigned = "assigned";

    public const string InProgress = "in_progress";

    public const string Completed = "completed";
}

public static class SprintTaskAssignmentModes
{
    public const string Auto = "auto";

    public const string Manual = "manual";
}

public static class SprintTaskAssigneeTypes
{
    public const int Employee = 0;

    public const int DigitalWorker = 1;
}

public static class SprintBugStatuses
{
    public const string Open = "open";

    public const string Fixing = "fixing";

    public const string FixedReadyForRegression = "fixed_ready_regression";

    public const string Closed = "closed";
}

public static class SprintBugSeverities
{
    public const string Critical = "critical";

    public const string Major = "major";

    public const string Minor = "minor";

    public const string Trivial = "trivial";
}

public static class SprintTaskLeaseStatuses
{
    public const string Active = "active";

    public const string Completed = "completed";

    public const string Released = "released";
}

public static class SprintTaskTargetTypes
{
    public const string Requirement = "requirement";

    public const string Bug = "bug";

    public const string DevelopmentTask = "development_task";
}
