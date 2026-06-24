using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Model.Modules.Agile.Workers;

namespace AgentSprint.Service.Services.AgileServices;

public interface ICodeAuditService
{
    Task<CodeAuditTaskResult> CreateTaskAsync(CreateCodeAuditTaskRequest request, string userId);

    Task<IReadOnlyList<CodeAuditTaskResult>> ListTasksAsync(
        string? projectId = null,
        string? status = null,
        string? auditTargetType = null,
        string? keyword = null);

    Task<IReadOnlyList<CodeAuditResultListItem>> ListResultsAsync(
        string? projectId = null,
        string? status = null,
        string? keyword = null);

    Task<IReadOnlyList<CodeAuditFileResult>> ListFilesAsync(
        string? projectId = null,
        string? branch = null,
        string? auditStatus = null,
        string? fileType = null,
        string? keyword = null);

    Task<CodeAuditTaskDetailResult> GetTaskAsync(string id);

    Task<CodeAuditReleaseReportResult> GetReleaseReportAsync(string id);

    Task<CodeAuditTaskResult> CancelTaskAsync(string id, string userId);

    Task<CodeAuditTaskResult> RetryTaskAsync(string id, string userId);

    Task<WorkerCommandResult> CreateIndexSyncCommandAsync(CreateCodeAuditIndexSyncCommandRequest request, string userId);

    Task<CodeAuditExecutionContextResult> GetExecutionContextAsync(string id, string workerId);

    Task<CodeAuditExecutionContextResult> PrepareExecutionContextAsync(
        string id,
        string workerId,
        PrepareCodeAuditContextRequest request);

    Task<CodeAuditFileIndexSyncResult> SyncFileIndexAsync(SyncCodeAuditFileIndexRequest request);

    Task<CodeAuditResultResult?> GetResultAsync(string taskId);

    Task<CodeAuditTaskResult> MarkTaskRunningAsync(string taskId, string? workerRunId = null);

    Task<CodeAuditTaskDetailResult> CompleteTaskAsync(string taskId, CompleteCodeAuditTaskRequest request);
}
