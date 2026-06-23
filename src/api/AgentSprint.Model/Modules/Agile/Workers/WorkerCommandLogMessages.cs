namespace AgentSprint.Model.Modules.Agile.Workers;

/// <summary>
/// <para>zh-cn: 表示 Worker 通过 Akka 投递到平台的命令日志增量消息。消息按命令维度携带会话、实例、运行编号和递增序号，平台接收后写入 Redis 实时缓冲；当 <paramref name="Completed" /> 为 true 时，平台会沿用运行时服务的完成归档逻辑写入数据库并清理实时缓冲。</para>
/// <para>en-us: Represents an incremental command-log message that a Worker sends to the platform through Akka. The message carries session, instance, run, and monotonic sequence data for one command; the platform writes it into the Redis live buffer, and when <paramref name="Completed" /> is true the runtime service archives it to the database and clears the live buffer.</para>
/// </summary>
/// <param name="WorkerId">
/// <para>zh-cn: 发送日志的 Worker 编号，用于平台校验 Worker、命令和会话归属。</para>
/// <para>en-us: Worker identifier used by the platform to validate ownership of the command and session.</para>
/// </param>
/// <param name="SessionId">
/// <para>zh-cn: Worker 当前会话编号。</para>
/// <para>en-us: Current Worker session identifier.</para>
/// </param>
/// <param name="InstanceId">
/// <para>zh-cn: Worker 实例编号，用于区分同一 Worker 的不同部署实例。</para>
/// <para>en-us: Worker instance identifier used to distinguish deployments of the same Worker.</para>
/// </param>
/// <param name="CommandId">
/// <para>zh-cn: 命令编号，Redis 实时缓冲和数据库归档都按该编号聚合日志。</para>
/// <para>en-us: Command identifier used as the aggregation key for both Redis live buffering and database archival.</para>
/// </param>
/// <param name="RunId">
/// <para>zh-cn: 可选运行编号，命令尚未创建运行记录时可以为空。</para>
/// <para>en-us: Optional run identifier; it may be null before the command creates a run record.</para>
/// </param>
/// <param name="Sequence">
/// <para>zh-cn: Worker 侧递增序号，用于平台跳过重复或倒序的日志片段。</para>
/// <para>en-us: Monotonic Worker-side sequence used by the platform to skip duplicate or stale chunks.</para>
/// </param>
/// <param name="Chunk">
/// <para>zh-cn: 日志增量文本；完成标记可以不携带文本。</para>
/// <para>en-us: Incremental log text; completion markers may omit text.</para>
/// </param>
/// <param name="Completed">
/// <para>zh-cn: 是否为完成标记。完成后平台会持久化整段日志并按实例保留最近 200 条数据库记录。</para>
/// <para>en-us: Indicates a completion marker. On completion the platform persists the full log and retains the latest 200 database records per instance.</para>
/// </param>
/// <param name="StartedAt">
/// <para>zh-cn: Worker 开始收集该命令日志的 UTC 时间。</para>
/// <para>en-us: UTC time when the Worker started collecting logs for the command.</para>
/// </param>
/// <param name="CompletedAt">
/// <para>zh-cn: Worker 完成该命令日志收集的 UTC 时间；未完成时为空。</para>
/// <para>en-us: UTC time when the Worker finished collecting logs; null while the command is still running.</para>
/// </param>
public sealed record WorkerCommandLogChunkMessage(
    string WorkerId,
    string SessionId,
    string InstanceId,
    string CommandId,
    string? RunId,
    long Sequence,
    string? Chunk,
    bool Completed,
    DateTime? StartedAt,
    DateTime? CompletedAt);

/// <summary>
/// <para>zh-cn: 表示平台端已经通过 Akka 收到并处理 Worker 命令日志增量，用于 Worker 判断是否可以跳过 HTTP 兜底。</para>
/// <para>en-us: Acknowledges that the platform received and processed a Worker command-log chunk through Akka, allowing the Worker to skip the HTTP fallback only after a confirmed append.</para>
/// </summary>
/// <param name="CommandId">
/// <para>zh-cn: 已确认的命令编号。</para>
/// <para>en-us: Command identifier that was acknowledged.</para>
/// </param>
/// <param name="RunId">
/// <para>zh-cn: 已确认的运行编号；命令尚未创建运行记录时可以为空。</para>
/// <para>en-us: Acknowledged run identifier; it may be null before a command creates a run record.</para>
/// </param>
/// <param name="Sequence">
/// <para>zh-cn: 已确认的日志片段序号。</para>
/// <para>en-us: Acknowledged log chunk sequence.</para>
/// </param>
/// <param name="Completed">
/// <para>zh-cn: 是否为完成标记的确认。</para>
/// <para>en-us: Indicates whether this acknowledgement is for a completion marker.</para>
/// </param>
public sealed record WorkerCommandLogAckMessage(
    string CommandId,
    string? RunId,
    long Sequence,
    bool Completed);
