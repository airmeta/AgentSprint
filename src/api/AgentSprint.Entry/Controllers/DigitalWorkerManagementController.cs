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
