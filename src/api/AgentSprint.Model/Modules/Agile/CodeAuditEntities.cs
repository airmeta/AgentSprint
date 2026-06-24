using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using AgentSprint.Model.Modules.Common;

namespace AgentSprint.Model.Modules.Agile;

[Table("code_audit_task")]
public sealed class CodeAuditTaskEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string GitRepositoryId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Branch { get; set; } = string.Empty;

    [MaxLength(64)]
    public string WorkerId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string AuditTargetType { get; set; } = CodeAuditTargetTypes.DevelopmentTask;

    [MaxLength(64)]
    public string? TargetId { get; set; }

    [MaxLength(64)]
    public string? SourceTaskId { get; set; }

    [MaxLength(64)]
    public string? SourceCommandId { get; set; }

    [MaxLength(64)]
    public string? AuditCommandId { get; set; }

    [MaxLength(64)]
    public string? SourceRunId { get; set; }

    [MaxLength(64)]
    public string? SourceGitCommitId { get; set; }

    [MaxLength(64)]
    public string? BaseCommitId { get; set; }

    [MaxLength(64)]
    public string? HeadCommitId { get; set; }

    [MaxLength(64)]
    public string? CurrentBranchHeadCommitId { get; set; }

    [MaxLength(64)]
    public string? RequirementId { get; set; }

    [MaxLength(64)]
    public string? ModuleId { get; set; }

    [Column(TypeName = "text")]
    public string? ScopeJson { get; set; }

    [MaxLength(1024)]
    public string? SelectedSkillIds { get; set; }

    [MaxLength(2048)]
    public string? Instruction { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = CodeAuditTaskStatuses.Pending;

    [MaxLength(32)]
    public string? Conclusion { get; set; }

    [MaxLength(1024)]
    public string? WorkspaceDirtyReason { get; set; }

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

[Table("code_audit_result")]
public sealed class CodeAuditResultEntity : EntityBase
{
    [MaxLength(64)]
    public string AuditTaskId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? WorkerCommandId { get; set; }

    [MaxLength(64)]
    public string? WorkerRunId { get; set; }

    [MaxLength(64)]
    public string? GitCommitId { get; set; }

    [MaxLength(128)]
    public string? Branch { get; set; }

    [Column(TypeName = "text")]
    public string? ChangedFilesJson { get; set; }

    [Column(TypeName = "longtext")]
    public string? PromptSnapshot { get; set; }

    [Column(TypeName = "longtext")]
    public string? SkillContextSnapshot { get; set; }

    [Column(TypeName = "longtext")]
    public string? RawResult { get; set; }

    [Column(TypeName = "longtext")]
    public string? StructuredResultJson { get; set; }

    [MaxLength(32)]
    public string? Conclusion { get; set; }

    [Column(TypeName = "longtext")]
    public string? IssuesJson { get; set; }

    [Column(TypeName = "longtext")]
    public string? AnnotationIssuesJson { get; set; }

    [Column(TypeName = "longtext")]
    public string? ManualCheckItemsJson { get; set; }
}

[Table("code_audit_file")]
public sealed class CodeAuditFileEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string GitRepositoryId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Branch { get; set; } = string.Empty;

    [MaxLength(32)]
    public string FileType { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(64)]
    public string FilePathHash { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? FileContentHash { get; set; }

    [MaxLength(32)]
    public string AuditStatus { get; set; } = CodeAuditFileStatuses.NotAudited;

    [MaxLength(64)]
    public string? LastAuditTaskId { get; set; }

    [MaxLength(64)]
    public string? LastAuditResultId { get; set; }

    public DateTime? LastAuditAt { get; set; }

    [MaxLength(64)]
    public string? LastCommitId { get; set; }

    public int IssueCount { get; set; }

    public int BlockingIssueCount { get; set; }

    public int HighIssueCount { get; set; }

    public int MediumIssueCount { get; set; }

    public int LowIssueCount { get; set; }

    [MaxLength(1024)]
    public string? Summary { get; set; }
}

public static class CodeAuditTargetTypes
{
    public const string DevelopmentTask = "development_task";

    public const string Files = "files";

    public const string Folders = "folders";

    public const string RequirementModule = "requirement_module";

    public const string FeatureDescription = "feature_description";

    public const string ReleasePreflight = "release_preflight";
}

public static class CodeAuditTaskStatuses
{
    public const string Pending = "pending";

    public const string Running = "running";

    public const string Passed = "passed";

    public const string NeedsChanges = "needs_changes";

    public const string Blocked = "blocked";

    public const string Failed = "failed";

    public const string Cancelled = "cancelled";
}

public static class CodeAuditConclusions
{
    public const string Passed = "passed";

    public const string NeedsChanges = "needs_changes";

    public const string Blocked = "blocked";
}

public static class CodeAuditFileStatuses
{
    public const string Normal = "normal";

    public const string Abnormal = "abnormal";

    public const string NotAudited = "not_audited";

    public const string Deleted = "deleted";
}
