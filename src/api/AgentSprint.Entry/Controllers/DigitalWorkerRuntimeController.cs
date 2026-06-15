using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Route("worker-runtime")]
public sealed class DigitalWorkerRuntimeController : ControllerBase
{
    private readonly IDigitalWorkerRuntimeService _service;

    /// <summary>
    /// zh-cn: 创建数字员工运行时控制器，向 AgentSprint.Worker 暴露注册、心跳、命令 ACK、运行记录和事件上报接口。
    /// en-us: Creates the digital-worker runtime controller exposing registration, heartbeat, command ACK, run records, and event reporting APIs to AgentSprint.Worker.
    /// </summary>
    /// <param name="service">
    /// zh-cn: 数字员工运行时服务。
    /// en-us: Digital-worker runtime service.
    /// </param>
    public DigitalWorkerRuntimeController(IDigitalWorkerRuntimeService service)
    {
        _service = service;
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
}
