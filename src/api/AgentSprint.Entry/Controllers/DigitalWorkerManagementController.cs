using System.Security.Claims;

using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Authorize(Roles = "super")]
[Route("workers")]
public sealed class DigitalWorkerManagementController : ControllerBase
{
    private readonly IDigitalWorkerManagementService _service;

    /// <summary>
    /// zh-cn: 创建数字员工管理控制器，向管理端暴露 Worker 主档、命令、会话、运行记录和事件查询接口。
    /// en-us: Creates the digital-worker management controller exposing worker profiles, commands, sessions, runs, and event queries to the admin side.
    /// </summary>
    /// <param name="service">
    /// zh-cn: 数字员工管理服务。
    /// en-us: Digital-worker management service.
    /// </param>
    public DigitalWorkerManagementController(IDigitalWorkerManagementService service)
    {
        _service = service;
    }

    [HttpPost]
    public Task<ActionResult<ApiResponse<DigitalWorkerResult>>> CreateWorker(CreateDigitalWorkerRequest request)
    {
        return Execute(() => _service.CreateWorkerAsync(request, GetUserId()));
    }

    [HttpPut("{id}")]
    public Task<ActionResult<ApiResponse<DigitalWorkerResult>>> UpdateWorker(
        string id,
        UpdateDigitalWorkerRequest request)
    {
        return Execute(() => _service.UpdateWorkerAsync(id, request));
    }

    [HttpGet]
    public async Task<ApiResponse<IReadOnlyList<DigitalWorkerResult>>> ListWorkers(
        [FromQuery] string? status,
        [FromQuery] string? workerType,
        [FromQuery] string? keyword)
    {
        return ApiResponse<IReadOnlyList<DigitalWorkerResult>>.Ok(
            await _service.ListWorkersAsync(status, workerType, keyword));
    }

    [HttpGet("{id}/detail")]
    public Task<ActionResult<ApiResponse<DigitalWorkerDetailResult>>> GetWorkerDetail(string id)
    {
        return Execute(() => _service.GetWorkerDetailAsync(id));
    }

    [HttpPost("{id}/status")]
    public Task<ActionResult<ApiResponse<DigitalWorkerResult>>> SetWorkerStatus(
        string id,
        SetDigitalWorkerStatusRequest request)
    {
        return Execute(() => _service.SetWorkerStatusAsync(id, request));
    }

    [HttpPost("commands")]
    public Task<ActionResult<ApiResponse<WorkerCommandResult>>> CreateCommand(CreateWorkerCommandRequest request)
    {
        return Execute(() => _service.CreateCommandAsync(request, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 查询 Worker 命令审计列表，管理端可按数字员工、会话、命令类型和状态过滤历史命令。
    /// en-us: Lists Worker commands for the admin audit view with optional worker, session, command-type, and status filters.
    /// </summary>
    [HttpGet("commands")]
    public async Task<ApiResponse<IReadOnlyList<WorkerCommandResult>>> ListCommands(
        [FromQuery] string? workerId,
        [FromQuery] string? sessionId,
        [FromQuery] string? commandType,
        [FromQuery] string? status)
    {
        return ApiResponse<IReadOnlyList<WorkerCommandResult>>.Ok(
            await _service.ListCommandsAsync(workerId, sessionId, commandType, status));
    }

    /// <summary>
    /// zh-cn: 回放历史 Worker 命令，复制原命令类型和载荷并创建一条新的待领取命令。
    /// en-us: Replays a historical Worker command by copying its type and payload into a new pending command.
    /// </summary>
    [HttpPost("commands/{commandId}/replay")]
    public Task<ActionResult<ApiResponse<WorkerCommandResult>>> ReplayCommand(string commandId)
    {
        return Execute(() => _service.ReplayCommandAsync(commandId, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 鑾峰彇鍛戒护鏃ュ織蹇収锛岀鐞嗙鍙敤浜庤疆璇㈠疄鏃剁洃鎺?Worker 杈撳嚭銆?
    /// en-us: Gets a command-log snapshot for management polling of live Worker output.
    /// </summary>
    [HttpGet("commands/{commandId}/log")]
    public Task<ActionResult<ApiResponse<WorkerCommandLogSnapshotResult?>>> GetCommandLogSnapshot(string commandId)
    {
        return Execute(() => _service.GetCommandLogSnapshotAsync(commandId));
    }

    /// <summary>
    /// zh-cn: 閫氳繃 SSE 鎸佺画杈撳嚭鍛戒护鏃ュ織蹇収锛屽墠绔彲鐢ㄤ簬浠诲姟瀹炴椂鐩戞帶椤甸潰銆?
    /// en-us: Streams command-log snapshots through SSE so the frontend can monitor task output in real time.
    /// </summary>
    [HttpGet("commands/{commandId}/log/stream")]
    public async Task StreamCommandLog(string commandId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.ContentType = "text/event-stream";

        var lastSequence = -1L;
        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = await _service.GetCommandLogSnapshotAsync(commandId);
            if (snapshot is not null && snapshot.LastSequence != lastSequence)
            {
                lastSequence = snapshot.LastSequence;
                await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(snapshot)}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                if (snapshot.Completed)
                {
                    break;
                }
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    [HttpGet("sessions")]
    public async Task<ApiResponse<IReadOnlyList<WorkerSessionResult>>> ListSessions(
        [FromQuery] string? workerId,
        [FromQuery] string? status)
    {
        return ApiResponse<IReadOnlyList<WorkerSessionResult>>.Ok(
            await _service.ListSessionsAsync(workerId, status));
    }

    [HttpGet("runs")]
    public async Task<ApiResponse<IReadOnlyList<WorkerRunResult>>> ListRuns(
        [FromQuery] string? workerId,
        [FromQuery] string? sessionId,
        [FromQuery] string? targetType,
        [FromQuery] string? targetId,
        [FromQuery] string? status)
    {
        return ApiResponse<IReadOnlyList<WorkerRunResult>>.Ok(
            await _service.ListRunsAsync(workerId, sessionId, targetType, targetId, status));
    }

    [HttpGet("events")]
    public async Task<ApiResponse<IReadOnlyList<WorkerEventResult>>> ListEvents(
        [FromQuery] string? workerId,
        [FromQuery] string? sessionId,
        [FromQuery] string? runId,
        [FromQuery] string? eventType)
    {
        return ApiResponse<IReadOnlyList<WorkerEventResult>>.Ok(
            await _service.ListEventsAsync(workerId, sessionId, runId, eventType));
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
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<T>.Error("Authentication is required.", 401));
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
    }
}
