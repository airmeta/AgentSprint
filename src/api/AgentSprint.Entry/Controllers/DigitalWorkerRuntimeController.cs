using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Route("worker-runtime")]
public sealed class DigitalWorkerRuntimeController : ControllerBase
{
    private readonly IDigitalWorkerRuntimeService _service;
    private readonly ICodeAuditService _codeAuditService;

    /// <summary>
    /// zh-cn: 创建数字员工运行时控制器，向 AgentSprint.Worker 暴露注册、心跳、命令 ACK、运行记录和事件上报接口。
    /// en-us: Creates the digital-worker runtime controller exposing registration, heartbeat, command ACK, run records, and event reporting APIs to AgentSprint.Worker.
    /// </summary>
    /// <param name="service">
    /// zh-cn: 数字员工运行时服务。
    /// en-us: Digital-worker runtime service.
    /// </param>
    public DigitalWorkerRuntimeController(
        IDigitalWorkerRuntimeService service,
        ICodeAuditService? codeAuditService = null)
    {
        _service = service;
        _codeAuditService = codeAuditService ?? new RuntimeCodeAuditServiceUnavailable();
    }

    [HttpGet("config/{workerId}")]
    public Task<ActionResult<ApiResponse<WorkerRuntimeConfigResult>>> GetRuntimeConfig(string workerId)
    {
        return Execute(() => _service.GetRuntimeConfigAsync(workerId));
    }

    /// <summary>
    /// zh-cn: 使用 Authorization Bearer 中的 Agent Token 获取数字员工平台托管配置。独立部署的 AgentSprint.Worker 只需要配置 API 地址和 Agent Token，该接口会反查绑定的数字员工主档并返回 WorkerId、目录、Codex、MCP、轮询和冒烟策略。
    /// en-us: Gets platform-managed digital-worker configuration from the Agent Token in Authorization Bearer. A standalone AgentSprint.Worker only needs the API URL and Agent Token; this endpoint resolves the bound digital-worker profile and returns WorkerId, directories, Codex, MCP, polling, and smoke settings.
    /// </summary>
    /// <returns>
    /// zh-cn: 当前 Agent Token 绑定的数字员工运行配置。
    /// en-us: Runtime configuration for the digital worker bound to the current Agent Token.
    /// </returns>
    [HttpGet("config")]
    public Task<ActionResult<ApiResponse<WorkerRuntimeConfigResult>>> GetRuntimeConfig()
    {
        return Execute(() => _service.GetRuntimeConfigByAgentTokenAsync(ReadBearerToken()));
    }

    [HttpGet("startup-probes")]
    public async Task<ApiResponse<IReadOnlyList<StartupProbeConfigResult>>> ListStartupProbeConfigs()
    {
        return ApiResponse<IReadOnlyList<StartupProbeConfigResult>>.Ok(
            await _service.ListStartupProbeConfigsAsync());
    }

    /// <summary>
    /// zh-cn: 为受控端数字员工生成任务或缺陷执行提示词；Worker 会把返回内容写入 codex exec，Codex 不需要也不应该通过 MCP 连接平台。
    /// en-us: Builds the task or bug execution prompt for the controlled digital worker; Worker writes the returned content into codex exec, and Codex does not need or use MCP to connect back to the platform.
    /// </summary>
    [HttpGet("work/{targetType}/{targetId}/prompt")]
    public async Task<ActionResult<ApiResponse<WorkerPromptResult>>> GetWorkPrompt(
        string targetType,
        string targetId)
    {
        return await Execute(async () =>
        {
            var workerId = await ReadWorkerIdFromBearerAsync();
            return await _service.GetWorkPromptAsync(workerId, targetType, targetId);
        });
    }

    /// <summary>
    /// zh-cn: Worker 在 Codex 成功退出后调用，按目标类型完成平台任务或缺陷状态回写，保持数字员工 API 链路闭环。
    /// en-us: Called by Worker after Codex exits successfully to complete the platform task or bug status update for the target type, keeping the digital-worker API flow closed.
    /// </summary>
    [HttpPost("work/{targetType}/{targetId}/complete")]
    public async Task<ActionResult<ApiResponse<WorkerWorkCompletionResult>>> CompleteWork(
        string targetType,
        string targetId)
    {
        return await Execute(async () =>
        {
            var workerId = await ReadWorkerIdFromBearerAsync();
            return await _service.CompleteWorkAsync(workerId, targetType, targetId);
        });
    }

    [HttpPost("register-session")]
    public Task<ActionResult<ApiResponse<WorkerSessionResult>>> RegisterSession(RegisterWorkerSessionRequest request)
    {
        return Execute(() => _service.RegisterSessionAsync(request));
    }

    [HttpPost("heartbeat")]
    public Task<ActionResult<ApiResponse<WorkerHeartbeatResult>>> Heartbeat(WorkerHeartbeatRequest request)
    {
        return Execute(() => _service.HeartbeatAsync(request));
    }

    [HttpPost("startup-probes/report")]
    public Task<ActionResult<ApiResponse<IReadOnlyList<StartupProbeResult>>>> ReportStartupProbeResults(
        ReportStartupProbeResultsRequest request)
    {
        return Execute(() => _service.ReportStartupProbeResultsAsync(request));
    }

    [HttpPost("commands/{id}/ack")]
    public Task<ActionResult<ApiResponse<WorkerCommandResult>>> AckCommand(
        string id,
        AckWorkerCommandRequest request)
    {
        return Execute(() => _service.AckCommandAsync(id, request));
    }

    [HttpPost("commands/{id}/start")]
    public Task<ActionResult<ApiResponse<WorkerCommandResult>>> StartCommand(
        string id,
        AckWorkerCommandRequest request)
    {
        return Execute(() => _service.StartCommandAsync(id, request));
    }

    [HttpPost("runs/start")]
    public Task<ActionResult<ApiResponse<WorkerRunResult>>> StartRun(StartWorkerRunRequest request)
    {
        return Execute(() => _service.StartRunAsync(request));
    }

    [HttpPost("runs/{id}/finish")]
    public Task<ActionResult<ApiResponse<WorkerRunResult>>> FinishRun(
        string id,
        FinishWorkerRunRequest request)
    {
        return Execute(() => _service.FinishRunAsync(id, request));
    }

    [HttpPost("events")]
    public Task<ActionResult<ApiResponse<WorkerEventResult>>> ReportEvent(ReportWorkerEventRequest request)
    {
        return Execute(() => _service.ReportEventAsync(request));
    }

    /// <summary>
    /// zh-cn: 鎺ユ敹 Worker 姣?200ms 宸﹀彸鎺ㄩ€佺殑鍛戒护鏃ュ織澧為噺锛屽苟鍦ㄧ粨鏉熸爣璁板埌杈炬椂瑙﹀彂鏃ュ織钀藉簱銆?
    /// en-us: Receives command-log chunks pushed by Worker about every 200 ms and persists the aggregated log when the completion marker arrives.
    /// </summary>
    [HttpPost("command-logs/append")]
    public async Task<ActionResult<ApiResponse<WorkerCommandLogSnapshotResult>>> AppendCommandLog(AppendWorkerCommandLogRequest request)
    {
        return await Execute(async () =>
        {
            var workerId = await ReadWorkerIdFromBearerAsync();
            return await _service.AppendCommandLogAsync(workerId, request);
        });
    }

    [HttpGet("code-audit/{taskId}")]
    public Task<ActionResult<ApiResponse<CodeAuditTaskDetailResult>>> GetCodeAuditTask(string taskId)
    {
        return Execute(() => _codeAuditService.GetTaskAsync(taskId));
    }

    [HttpGet("code-audit/{taskId}/context")]
    public async Task<ActionResult<ApiResponse<CodeAuditExecutionContextResult>>> GetCodeAuditExecutionContext(string taskId)
    {
        return await Execute(async () =>
        {
            var workerId = await ReadWorkerIdFromBearerAsync();
            return await _codeAuditService.GetExecutionContextAsync(taskId, workerId);
        });
    }

    [HttpPost("code-audit/{taskId}/running")]
    public Task<ActionResult<ApiResponse<CodeAuditTaskResult>>> MarkCodeAuditTaskRunning(
        string taskId,
        [FromQuery] string? workerRunId = null)
    {
        return Execute(() => _codeAuditService.MarkTaskRunningAsync(taskId, workerRunId));
    }

    [HttpPost("code-audit/{taskId}/prepared")]
    public async Task<ActionResult<ApiResponse<CodeAuditExecutionContextResult>>> PrepareCodeAuditExecutionContext(
        string taskId,
        PrepareCodeAuditContextRequest request)
    {
        return await Execute(async () =>
        {
            var workerId = await ReadWorkerIdFromBearerAsync();
            return await _codeAuditService.PrepareExecutionContextAsync(taskId, workerId, request);
        });
    }

    [HttpPost("code-audit/file-index/sync")]
    public Task<ActionResult<ApiResponse<CodeAuditFileIndexSyncResult>>> SyncCodeAuditFileIndex(
        SyncCodeAuditFileIndexRequest request)
    {
        return Execute(() => _codeAuditService.SyncFileIndexAsync(request));
    }

    [HttpPost("code-audit/{taskId}/complete")]
    public Task<ActionResult<ApiResponse<CodeAuditTaskDetailResult>>> CompleteCodeAuditTask(
        string taskId,
        CompleteCodeAuditTaskRequest request)
    {
        return Execute(() => _codeAuditService.CompleteTaskAsync(taskId, request));
    }

    private async Task<ActionResult<ApiResponse<T>>> Execute<T>(Func<Task<T>> action)
    {
        try
        {
            return ApiResponse<T>.Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<T>.Error(ex.Message, 400));
        }
    }

    private string ReadBearerToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authorization[bearerPrefix.Length..].Trim();
        }

        throw new InvalidOperationException("Agent Token is required.");
    }

    private async Task<string> ReadWorkerIdFromBearerAsync()
    {
        return (await _service.GetRuntimeConfigByAgentTokenAsync(ReadBearerToken())).WorkerId;
    }

    private sealed class RuntimeCodeAuditServiceUnavailable : ICodeAuditService
    {
        public Task<CodeAuditTaskResult> CreateTaskAsync(CreateCodeAuditTaskRequest request, string userId)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<IReadOnlyList<CodeAuditTaskResult>> ListTasksAsync(
            string? projectId = null,
            string? status = null,
            string? auditTargetType = null,
            string? keyword = null)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<IReadOnlyList<CodeAuditResultListItem>> ListResultsAsync(
            string? projectId = null,
            string? status = null,
            string? keyword = null)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<IReadOnlyList<CodeAuditFileResult>> ListFilesAsync(
            string? projectId = null,
            string? branch = null,
            string? auditStatus = null,
            string? fileType = null,
            string? keyword = null)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<CodeAuditTaskDetailResult> GetTaskAsync(string id)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<CodeAuditReleaseReportResult> GetReleaseReportAsync(string id)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<CodeAuditTaskResult> CancelTaskAsync(string id, string userId)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<CodeAuditTaskResult> RetryTaskAsync(string id, string userId)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<WorkerCommandResult> CreateIndexSyncCommandAsync(CreateCodeAuditIndexSyncCommandRequest request, string userId)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<CodeAuditExecutionContextResult> GetExecutionContextAsync(string id, string workerId)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<CodeAuditExecutionContextResult> PrepareExecutionContextAsync(
            string id,
            string workerId,
            PrepareCodeAuditContextRequest request)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<CodeAuditFileIndexSyncResult> SyncFileIndexAsync(SyncCodeAuditFileIndexRequest request)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<CodeAuditResultResult?> GetResultAsync(string taskId)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<CodeAuditTaskResult> MarkTaskRunningAsync(string taskId, string? workerRunId = null)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }

        public Task<CodeAuditTaskDetailResult> CompleteTaskAsync(string taskId, CompleteCodeAuditTaskRequest request)
        {
            throw new InvalidOperationException("Code audit service is not configured.");
        }
    }
}
