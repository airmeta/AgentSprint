using System.Security.Claims;

using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Authorize]
[Route("code-audit")]
public sealed class CodeAuditController : ControllerBase
{
    private readonly ICodeAuditService _service;

    public CodeAuditController(ICodeAuditService service)
    {
        _service = service;
    }

    [HttpPost("tasks")]
    public async Task<ActionResult<ApiResponse<CodeAuditTaskResult>>> CreateTask(CreateCodeAuditTaskRequest request)
    {
        try
        {
            return ApiResponse<CodeAuditTaskResult>.Ok(await _service.CreateTaskAsync(request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CodeAuditTaskResult>.Error(ex.Message, 400));
        }
    }

    [HttpGet("tasks")]
    public async Task<ApiResponse<IReadOnlyList<CodeAuditTaskResult>>> ListTasks(
        [FromQuery] string? projectId,
        [FromQuery] string? status,
        [FromQuery] string? auditTargetType,
        [FromQuery] string? keyword)
    {
        return ApiResponse<IReadOnlyList<CodeAuditTaskResult>>.Ok(
            await _service.ListTasksAsync(projectId, status, auditTargetType, keyword));
    }

    [HttpGet("tasks/{id}")]
    public async Task<ActionResult<ApiResponse<CodeAuditTaskDetailResult>>> GetTask(string id)
    {
        try
        {
            return ApiResponse<CodeAuditTaskDetailResult>.Ok(await _service.GetTaskAsync(id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CodeAuditTaskDetailResult>.Error(ex.Message, 400));
        }
    }

    [HttpGet("tasks/{id}/release-report")]
    public async Task<ActionResult<ApiResponse<CodeAuditReleaseReportResult>>> GetReleaseReport(string id)
    {
        try
        {
            return ApiResponse<CodeAuditReleaseReportResult>.Ok(await _service.GetReleaseReportAsync(id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CodeAuditReleaseReportResult>.Error(ex.Message, 400));
        }
    }

    [HttpPost("tasks/{id}/cancel")]
    public async Task<ActionResult<ApiResponse<CodeAuditTaskResult>>> CancelTask(string id)
    {
        try
        {
            return ApiResponse<CodeAuditTaskResult>.Ok(await _service.CancelTaskAsync(id, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CodeAuditTaskResult>.Error(ex.Message, 400));
        }
    }

    [HttpPost("tasks/{id}/retry")]
    public async Task<ActionResult<ApiResponse<CodeAuditTaskResult>>> RetryTask(string id)
    {
        try
        {
            return ApiResponse<CodeAuditTaskResult>.Ok(await _service.RetryTaskAsync(id, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CodeAuditTaskResult>.Error(ex.Message, 400));
        }
    }

    [HttpGet("tasks/{id}/result")]
    public async Task<ActionResult<ApiResponse<CodeAuditResultResult?>>> GetResult(string id)
    {
        try
        {
            return ApiResponse<CodeAuditResultResult?>.Ok(await _service.GetResultAsync(id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CodeAuditResultResult?>.Error(ex.Message, 400));
        }
    }

    [HttpGet("results")]
    public async Task<ApiResponse<IReadOnlyList<CodeAuditResultListItem>>> ListResults(
        [FromQuery] string? projectId,
        [FromQuery] string? status,
        [FromQuery] string? keyword)
    {
        return ApiResponse<IReadOnlyList<CodeAuditResultListItem>>.Ok(
            await _service.ListResultsAsync(projectId, status, keyword));
    }

    [HttpGet("files")]
    public async Task<ApiResponse<IReadOnlyList<CodeAuditFileResult>>> ListFiles(
        [FromQuery] string? projectId,
        [FromQuery] string? branch,
        [FromQuery] string? auditStatus,
        [FromQuery] string? fileType,
        [FromQuery] string? keyword)
    {
        return ApiResponse<IReadOnlyList<CodeAuditFileResult>>.Ok(
            await _service.ListFilesAsync(projectId, branch, auditStatus, fileType, keyword));
    }

    [HttpPost("file-index/sync-commands")]
    public async Task<ActionResult<ApiResponse<WorkerCommandResult>>> CreateIndexSyncCommand(CreateCodeAuditIndexSyncCommandRequest request)
    {
        try
        {
            return ApiResponse<WorkerCommandResult>.Ok(await _service.CreateIndexSyncCommandAsync(request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<WorkerCommandResult>.Error(ex.Message, 400));
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    }
}
