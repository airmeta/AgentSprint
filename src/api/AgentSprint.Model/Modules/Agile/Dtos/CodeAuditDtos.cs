namespace AgentSprint.Model.Modules.Agile.Dtos;

public sealed record CreateCodeAuditTaskRequest(
    string ProjectId,
    string WorkerId,
    string AuditTargetType,
    string? TargetId = null,
    string? ScopeJson = null,
    IReadOnlyList<string>? SelectedSkillIds = null,
    string? Instruction = null,
    string? Branch = null);

public sealed record CompleteCodeAuditTaskRequest(
    string Status,
    string? Conclusion = null,
    string? WorkerRunId = null,
    string? GitCommitId = null,
    string? Branch = null,
    string? ChangedFilesJson = null,
    string? PromptSnapshot = null,
    string? SkillContextSnapshot = null,
    string? RawResult = null,
    string? StructuredResultJson = null,
    string? IssuesJson = null,
    string? AnnotationIssuesJson = null,
    string? ManualCheckItemsJson = null,
    string? WorkspaceDirtyReason = null);

public sealed record PrepareCodeAuditContextRequest(
    string? WorkerRunId = null,
    string? Branch = null,
    string? BaseCommitId = null,
    string? HeadCommitId = null,
    string? CurrentBranchHeadCommitId = null,
    string? ChangedFilesJson = null,
    string? Diff = null,
    string? CodeContext = null,
    bool? SourceCommitReachable = null,
    bool? SourceCommitBehindHead = null,
    string? Warning = null);

public sealed record SyncCodeAuditFileIndexRequest(
    string ProjectId,
    string GitRepositoryId,
    string Branch,
    string? CommitId,
    IReadOnlyList<CodeAuditFileIndexItem> Files);

public sealed record CodeAuditFileIndexItem(
    string FilePath,
    string? FileContentHash = null,
    string? FileType = null);

public sealed record CodeAuditFileIndexSyncResult(
    int Total,
    int Created,
    int Updated,
    int Deleted);

public sealed record CodeAuditFileResult(
    string Id,
    string ProjectId,
    string GitRepositoryId,
    string Branch,
    string FileType,
    string FilePath,
    string? FileContentHash,
    string AuditStatus,
    string? LastAuditTaskId,
    string? LastAuditResultId,
    DateTime? LastAuditAt,
    string? LastCommitId,
    int IssueCount,
    int BlockingIssueCount,
    int HighIssueCount,
    int MediumIssueCount,
    int LowIssueCount,
    string? Summary,
    DateTime CreateTime,
    DateTime? UpdateTime);

public sealed record CreateCodeAuditIndexSyncCommandRequest(
    string ProjectId,
    string WorkerId,
    string? Branch = null);

public sealed record CodeAuditReleaseReportResult(
    string AuditTaskId,
    string ProjectId,
    string GitRepositoryId,
    string Branch,
    string Status,
    string? Conclusion,
    string? GitCommitId,
    string? BaseCommitId,
    string? HeadCommitId,
    string? CurrentBranchHeadCommitId,
    int ChangedFileCount,
    int IssueCount,
    int BlockingIssueCount,
    int HighIssueCount,
    int MediumIssueCount,
    int LowIssueCount,
    int ManualCheckCount,
    bool CanRelease,
    IReadOnlyList<string> BlockingSummaries,
    IReadOnlyList<string> ManualCheckItems,
    DateTime? CompletedAt);

public sealed record CodeAuditTaskResult(
    string Id,
    string ProjectId,
    string GitRepositoryId,
    string Branch,
    string WorkerId,
    string AuditTargetType,
    string? TargetId,
    string? SourceTaskId,
    string? SourceCommandId,
    string? AuditCommandId,
    string? SourceRunId,
    string? SourceGitCommitId,
    string? BaseCommitId,
    string? HeadCommitId,
    string? CurrentBranchHeadCommitId,
    string? RequirementId,
    string? ModuleId,
    string? ScopeJson,
    IReadOnlyList<string> SelectedSkillIds,
    string? Instruction,
    string Status,
    string? Conclusion,
    string? WorkspaceDirtyReason,
    string CreatedBy,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime CreateTime,
    DateTime? UpdateTime);

public sealed record CodeAuditResultResult(
    string Id,
    string AuditTaskId,
    string? WorkerCommandId,
    string? WorkerRunId,
    string? GitCommitId,
    string? Branch,
    string? ChangedFilesJson,
    string? PromptSnapshot,
    string? SkillContextSnapshot,
    string? RawResult,
    string? StructuredResultJson,
    string? Conclusion,
    string? IssuesJson,
    string? AnnotationIssuesJson,
    string? ManualCheckItemsJson,
    DateTime CreateTime,
    DateTime? UpdateTime);

public sealed record CodeAuditTaskDetailResult(
    CodeAuditTaskResult Task,
    CodeAuditResultResult? Result);

public sealed record CodeAuditResultListItem(
    CodeAuditTaskResult Task,
    CodeAuditResultResult? Result);

public sealed record CodeAuditExecutionContextResult(
    CodeAuditTaskResult Task,
    string ProjectCode,
    string ProjectName,
    string RepositoryUrl,
    string? RepositoryReference,
    string? RepositoryDefaultBranch,
    string? GitUsername,
    string? GitAccessToken,
    string? GitCommitAuthorName,
    string? GitCommitAuthorEmail,
    string TemplateCode,
    string TemplateName,
    string Prompt,
    string PromptSnapshot,
    string SkillContextSnapshot,
    string? SourceCommitId,
    string? SourceRunId,
    string? SourceCommandId,
    string? ChangedFilesJson,
    string? Diff,
    string? CodeContext,
    string? GitContextWarnings,
    string? ScopeDescription,
    string? TargetSummary);
