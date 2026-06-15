namespace AgentSprint.Service.Services.AgileServices;

public sealed record GitCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public interface IGitCommandRunner
{
    /// <summary>
    /// zh-cn: 在指定工作目录执行 Git 命令，返回退出码和标准输出；实现方负责隐藏敏感参数并处理进程超时。
    /// en-us: Runs a Git command in the specified working directory and returns exit code plus standard output; implementations are responsible for hiding sensitive arguments and handling process timeouts.
    /// </summary>
    Task<GitCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
