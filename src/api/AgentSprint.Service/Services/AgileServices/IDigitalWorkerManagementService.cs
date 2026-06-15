using AgentSprint.Model.Modules.Agile.Workers;

namespace AgentSprint.Service.Services.AgileServices;

public interface IDigitalWorkerManagementService
{
    /// <summary>
    /// zh-cn: 创建数字员工主档，绑定平台机器人账号、项目范围和受控端运行策略，供 AgentSprint.Worker 启动时注册使用。
    /// en-us: Creates a digital-worker profile binding the platform agent account, project scope, and controlled-worker runtime policy used by AgentSprint.Worker registration.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 数字员工基础配置。
    /// en-us: Digital-worker base configuration.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前创建人标识。
    /// en-us: Current creator identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 创建后的数字员工。
    /// en-us: Created digital worker.
    /// </returns>
    Task<DigitalWorkerResult> CreateWorkerAsync(CreateDigitalWorkerRequest request, string userId);

    /// <summary>
    /// zh-cn: 更新数字员工主档，保留唯一编码不变，并刷新项目、技能、心跳超时和启停状态等管理配置。
    /// en-us: Updates a digital-worker profile while preserving its unique code and refreshing project, skill, heartbeat-timeout, and status settings.
    /// </summary>
    Task<DigitalWorkerResult> UpdateWorkerAsync(string id, UpdateDigitalWorkerRequest request);

    /// <summary>
    /// zh-cn: 查询数字员工列表，可按状态、类型和关键词过滤，并附带最近更新时间供管理端列表展示。
    /// en-us: Lists digital workers with optional status, type, and keyword filters for management pages.
    /// </summary>
    Task<IReadOnlyList<DigitalWorkerResult>> ListWorkersAsync(string? status = null, string? workerType = null, string? keyword = null);

    /// <summary>
    /// zh-cn: 获取数字员工详情，聚合主档、最近会话、当前运行和待处理命令，供管理端详情抽屉使用。
    /// en-us: Gets a digital-worker detail view aggregating profile, latest session, current run, and pending commands for the management drawer.
    /// </summary>
    Task<DigitalWorkerDetailResult> GetWorkerDetailAsync(string id);

    /// <summary>
    /// zh-cn: 设置数字员工状态，禁用或维护中的 Worker 不允许新的运行时会话进入工作循环。
    /// en-us: Sets digital-worker status; disabled or maintenance workers are not allowed to enter the runtime work loop.
    /// </summary>
    Task<DigitalWorkerResult> SetWorkerStatusAsync(string id, SetDigitalWorkerStatusRequest request);

    /// <summary>
    /// zh-cn: 创建平台下发命令，命令会在 Worker 下一次心跳时返回并由运行时 ACK。
    /// en-us: Creates a platform command that is returned on the worker's next heartbeat and acknowledged by the runtime.
    /// </summary>
    Task<WorkerCommandResult> CreateCommandAsync(CreateWorkerCommandRequest request, string userId);

    /// <summary>
    /// zh-cn: 查询 Worker 会话列表，支持按 Worker 和状态过滤。
    /// en-us: Lists Worker sessions with optional worker and status filters.
    /// </summary>
    Task<IReadOnlyList<WorkerSessionResult>> ListSessionsAsync(string? workerId = null, string? status = null);

    /// <summary>
    /// zh-cn: 查询 Worker 运行记录，支持按 Worker、会话、目标和状态过滤。
    /// en-us: Lists Worker runs with optional worker, session, target, and status filters.
    /// </summary>
    Task<IReadOnlyList<WorkerRunResult>> ListRunsAsync(
        string? workerId = null,
        string? sessionId = null,
        string? targetType = null,
        string? targetId = null,
        string? status = null);

    /// <summary>
    /// zh-cn: 查询 Worker 审计事件，支持按 Worker、会话、运行和事件类型过滤。
    /// en-us: Lists Worker audit events with optional worker, session, run, and event-type filters.
    /// </summary>
    Task<IReadOnlyList<WorkerEventResult>> ListEventsAsync(
        string? workerId = null,
        string? sessionId = null,
        string? runId = null,
        string? eventType = null);
}
