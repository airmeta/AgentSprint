namespace AgentSprint.Worker.Models;

/// <summary>
/// <para>zh-cn:描述一次 Codex CLI 执行请求。调用方可以提供可选的进度上报委托，用于把长时间无输出、即将空闲超时等本地进程状态转成平台事件；该委托不会参与 Codex 命令行参数。</para>
/// <para>en-us:Describes one Codex CLI run request. Callers may provide an optional progress reporter that turns local process states such as long silence and pending idle timeout into platform events; the reporter is not passed to the Codex command line.</para>
/// </summary>
/// <param name="RunId">
/// <para>zh-cn:本地运行目录使用的运行 ID。</para>
/// <para>en-us:Run ID used for the local run directory.</para>
/// </param>
/// <param name="WorkingDirectory">
/// <para>zh-cn:Codex 执行时使用的工作目录。</para>
/// <para>en-us:Working directory used by Codex.</para>
/// </param>
/// <param name="Prompt">
/// <para>zh-cn:传递给 Codex CLI 的最终提示词。</para>
/// <para>en-us:Final prompt passed to the Codex CLI.</para>
/// </param>
/// <param name="SandboxMode">
/// <para>zh-cn:Codex 沙箱模式。</para>
/// <para>en-us:Codex sandbox mode.</para>
/// </param>
/// <param name="SkipGitRepoCheck">
/// <para>zh-cn:是否跳过 Codex 的 Git 仓库检查。</para>
/// <para>en-us:Whether Codex should skip its Git repository check.</para>
/// </param>
/// <param name="Timeout">
/// <para>zh-cn:整次运行的总超时时间。</para>
/// <para>en-us:Total timeout for the run.</para>
/// </param>
/// <param name="IdleTimeout">
/// <para>zh-cn:stdout/stderr 无输出的空闲超时时间；为空时使用执行器默认值。</para>
/// <para>en-us:Idle timeout for stdout/stderr silence; when null, the runner default is used.</para>
/// </param>
/// <param name="CodexExecutable">
/// <para>zh-cn:Codex 可执行文件路径或命令名。</para>
/// <para>en-us:Codex executable path or command name.</para>
/// </param>
/// <param name="ProgressReporter">
/// <para>zh-cn:可选进度上报委托。执行器会在检测到长时间无输出或空闲超时时调用；委托异常会被记录但不会中断 Codex 运行。</para>
/// <para>en-us:Optional progress reporter. The runner invokes it when long silence or idle timeout is detected; reporter failures are logged but do not interrupt the Codex run.</para>
/// </param>
public sealed record CodexRunRequest(
    string RunId,
    string WorkingDirectory,
    string Prompt,
    string SandboxMode,
    bool SkipGitRepoCheck,
    TimeSpan Timeout,
    TimeSpan? IdleTimeout = null,
    string? CodexExecutable = null,
    Func<CodexRunProgressEvent, CancellationToken, Task>? ProgressReporter = null,
    Func<string, string, CancellationToken, Task>? OutputReporter = null);
