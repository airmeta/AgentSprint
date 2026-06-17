namespace AgentSprint.Worker.Models;

/// <summary>
/// <para>zh-cn:表示 Codex CLI 运行期间由本地执行器观测到的进度或健康事件。该事件用于补足 stdout/stderr 没有输出时的平台可观测性，尤其是区分“Codex 已启动但尚未产生模型/网关输出”和真正的运行结束。</para>
/// <para>en-us:Represents a progress or health event observed by the local runner during a Codex CLI run. The event improves platform observability when stdout/stderr is silent, especially to distinguish "Codex has started but has not produced model or gateway output yet" from a completed run.</para>
/// </summary>
/// <param name="EventType">
/// <para>zh-cn:平台事件类型。</para>
/// <para>en-us:Platform event type.</para>
/// </param>
/// <param name="Level">
/// <para>zh-cn:事件级别，通常为 info、warn 或 error。</para>
/// <para>en-us:Event level, usually info, warn, or error.</para>
/// </param>
/// <param name="Message">
/// <para>zh-cn:面向审计日志的简短说明。</para>
/// <para>en-us:Short message for audit logs.</para>
/// </param>
/// <param name="ObservedAt">
/// <para>zh-cn:执行器观察到该事件的 UTC 时间。</para>
/// <para>en-us:UTC time when the runner observed the event.</para>
/// </param>
/// <param name="LastOutputAt">
/// <para>zh-cn:最近一次收到 stdout/stderr 的 UTC 时间；若还没有输出，则为进程启动后的初始观察时间。</para>
/// <para>en-us:UTC time of the most recent stdout/stderr output; when no output has arrived yet, this is the initial observation time after process start.</para>
/// </param>
/// <param name="IdleFor">
/// <para>zh-cn:距离最近输出已经过去的时长。</para>
/// <para>en-us:Elapsed time since the most recent output.</para>
/// </param>
/// <param name="IdleTimeout">
/// <para>zh-cn:本次运行配置的空闲超时阈值。</para>
/// <para>en-us:Configured idle-timeout threshold for this run.</para>
/// </param>
/// <param name="HasOutput">
/// <para>zh-cn:当前运行是否已经产生过任意 stdout/stderr 输出。</para>
/// <para>en-us:Whether the run has produced any stdout/stderr output.</para>
/// </param>
public sealed record CodexRunProgressEvent(
    string EventType,
    string Level,
    string Message,
    DateTimeOffset ObservedAt,
    DateTimeOffset LastOutputAt,
    TimeSpan IdleFor,
    TimeSpan IdleTimeout,
    bool HasOutput);
