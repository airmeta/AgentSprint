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

    [MaxLength(512)]
    public string? RepositoryUrl { get; set; }

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
