using AgentSprint.Model.Modules.Agile.Workers;

namespace AgentSprint.Service.Services.AgileServices;

public interface IDigitalWorkerRuntimeService
{
    /// <summary>
    /// zh-cn: 获取 Worker 首次上线或重载时使用的平台托管配置，返回运行参数、Codex 配置和绑定 Agent Token，供受控端减少本地文件配置。
    /// en-us: Gets the platform-managed configuration used by a Worker at first startup or reload, returning runtime options, Codex options, and the bound Agent Token so the controlled endpoint can minimize local file configuration.
    /// </summary>
    Task<WorkerRuntimeConfigResult> GetRuntimeConfigAsync(string workerId);

    /// <summary>
    /// zh-cn: 通过部署环境中提供的 Agent Token 解析数字员工身份并返回平台托管配置。受控端独立部署时只需要配置平台 API 地址和 Agent Token，Worker 编号、运行目录、Codex 模型、MCP 地址、轮询间隔和冒烟策略都从数字员工主档下发；令牌无效、过期、撤销或未绑定数字员工时会抛出业务异常。
    /// en-us: Resolves the digital-worker identity from the Agent Token supplied by deployment and returns platform-managed configuration. A standalone controlled endpoint only needs the platform API URL and Agent Token; worker id, runtime directories, Codex model, MCP endpoint, polling cadence, and smoke policy are delivered from the digital-worker profile. Invalid, expired, revoked, or unbound tokens raise a business exception.
    /// </summary>
    /// <param name="agentToken">
    /// zh-cn: 部署时注入的完整 Agent Token 明文，通常来自 Authorization Bearer 或 AgentSprint:AgentToken。
    /// en-us: Full Agent Token plaintext injected at deployment, usually from Authorization Bearer or AgentSprint:AgentToken.
    /// </param>
    /// <returns>
    /// zh-cn: 与该令牌绑定的数字员工运行配置。
    /// en-us: Runtime configuration bound to the token's digital-worker profile.
    /// </returns>
    Task<WorkerRuntimeConfigResult> GetRuntimeConfigByAgentTokenAsync(string agentToken);

    /// <summary>
    /// zh-cn: 为 Worker 托管的 Codex 执行生成数字员工专用提示词；该接口读取平台任务、需求、项目、Skill 和提示词模板，返回已渲染的完整 prompt，不要求 Codex 再连接 MCP。
    /// en-us: Builds the digital-worker prompt for a Worker-hosted Codex run; this API reads platform task, requirement, project, skill, and prompt-template data and returns a rendered prompt without requiring Codex to connect to MCP.
    /// </summary>
    Task<WorkerPromptResult> GetWorkPromptAsync(string workerId, string targetType, string targetId);

    /// <summary>
    /// zh-cn: 在 Codex 成功退出后由 Worker 调用，按目标类型回写敏捷任务或缺陷状态，保持受控端平台状态闭环。
    /// en-us: Called by Worker after Codex exits successfully to update the agile task or bug state for the target type, keeping the controlled endpoint state loop closed.
    /// </summary>
    Task<WorkerWorkCompletionResult> CompleteWorkAsync(string workerId, string targetType, string targetId);

    /// <summary>
    /// zh-cn: 注册 Worker 会话，校验数字员工主档状态，记录启动探针信息，并关闭同一 Worker 下旧的非终态会话。
    /// en-us: Registers a Worker session, validates the digital-worker profile status, stores startup probe data, and closes older non-terminal sessions for the same worker.
    /// </summary>
    Task<WorkerSessionResult> RegisterSessionAsync(RegisterWorkerSessionRequest request);

    /// <summary>
    /// zh-cn: 接收 Worker 心跳，刷新会话状态并返回待处理命令和下一次心跳间隔。
    /// en-us: Receives a Worker heartbeat, refreshes session state, and returns pending commands plus the next heartbeat interval.
    /// </summary>
    Task<WorkerHeartbeatResult> HeartbeatAsync(WorkerHeartbeatRequest request);

    /// <summary>
    /// zh-cn: ACK 平台下发命令，确保同一命令不会被重复进入执行态。
    /// en-us: Acknowledges a platform command so the same command does not repeatedly enter execution.
    /// </summary>
    Task<WorkerCommandResult> AckCommandAsync(string commandId, AckWorkerCommandRequest request);

    /// <summary>
    /// zh-cn: 标记 Worker 命令开始执行，通常在启动 Codex run 前调用。
    /// en-us: Marks a Worker command as running, usually before starting the Codex run.
    /// </summary>
    Task<WorkerCommandResult> StartCommandAsync(string commandId, AckWorkerCommandRequest request);

    /// <summary>
    /// zh-cn: 创建 Worker 运行记录，并把对应会话切换为 busy/current run；当运行状态为 running 且目标是开发任务时，同步把任务和需求标记为实际推进中。
    /// en-us: Creates a Worker run and switches the related session to busy/current-run state; when the run is running and targets a development task, it marks the task and requirement as actually in progress.
    /// </summary>
    Task<WorkerRunResult> StartRunAsync(StartWorkerRunRequest request);

    /// <summary>
    /// zh-cn: 完成 Worker 运行记录，回写退出码、超时和错误摘要，并释放会话 busy 状态。
    /// en-us: Finishes a Worker run by writing exit code, timeout, and error summary, then releases the session busy state.
    /// </summary>
    Task<WorkerRunResult> FinishRunAsync(string runId, FinishWorkerRunRequest request);

    /// <summary>
    /// zh-cn: 写入 Worker 生命周期或运行过程事件，用于管理端审计时间线。
    /// en-us: Writes a Worker lifecycle or run event for the management audit timeline.
    /// </summary>
    Task<WorkerEventResult> ReportEventAsync(ReportWorkerEventRequest request);
}
