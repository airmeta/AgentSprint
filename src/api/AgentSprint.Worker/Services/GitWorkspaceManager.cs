using AgentSprint.Worker.Models;

namespace AgentSprint.Worker.Services;

public sealed class GitWorkspaceManager
{
    private static readonly TimeSpan GitCommandTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// <para>zh-cn:创建 Worker 侧 Git 工作区管理器。该管理器在 Codex 启动前以确定性命令准备项目仓库，负责 clone、fetch、checkout、pull 以及状态采集，避免把拉取最新代码这类基础动作交给模型自行决定。</para>
    /// <para>en-us:Creates the Worker-side Git workspace manager. The manager prepares the project repository with deterministic commands before Codex starts, covering clone, fetch, checkout, pull, and status collection so base repository synchronization is not left to the model.</para>
    /// </summary>
    /// <summary>
    /// <para>zh-cn:准备指定项目的本地工作区。没有仓库地址时只创建目录并返回不可用状态；目录不是 Git 仓库时执行 clone；已有仓库时执行 fetch 和 pull，并按分支参数切换或跟踪远端分支。任何 Git 命令失败都会返回失败结果，调用方应阻止真实开发 run 继续进入 Codex。</para>
    /// <para>en-us:Prepares the local workspace for a project. When no repository URL is available it only creates the directory and reports repository unavailable; when the directory is not a Git repository it clones; when a repository exists it fetches and pulls, optionally checking out or tracking the requested branch. Any Git command failure returns a failed result and callers should stop real development runs before Codex starts.</para>
    /// </summary>
    /// <param name="workspaceRoot">
    /// <para>zh-cn:Worker 工作区根目录。</para>
    /// <para>en-us:Worker workspace root directory.</para>
    /// </param>
    /// <param name="projectCode">
    /// <para>zh-cn:项目编码，用作根目录下的项目文件夹名称；为空时使用 `_unscoped`。</para>
    /// <para>en-us:Project code used as the project folder name under the workspace root; `_unscoped` is used when it is empty.</para>
    /// </param>
    /// <param name="repositoryUrl">
    /// <para>zh-cn:平台返回的真实 Git 仓库地址，只在 Worker 进程内用于 Git 命令，不写入 Codex prompt。</para>
    /// <para>en-us:Real Git repository URL returned by the platform, used only inside the Worker process for Git commands and not written into the Codex prompt.</para>
    /// </param>
    /// <param name="branch">
    /// <para>zh-cn:可选目标分支。为空时保留 clone 默认分支或当前分支，并从当前上游拉取最新代码。</para>
    /// <para>en-us:Optional target branch. When empty, the clone default branch or current branch is kept and updated from its upstream.</para>
    /// </param>
    /// <param name="cancellationToken">
    /// <para>zh-cn:取消令牌。</para>
    /// <para>en-us:Cancellation token.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:工作区路径、仓库可用性、当前分支、提交号、脏状态和错误摘要。</para>
    /// <para>en-us:Workspace path, repository availability, current branch, commit, dirty state, and error summary.</para>
    /// </returns>
    public async Task<WorkspacePreparationResult> PrepareAsync(
        string workspaceRoot,
        string? projectCode,
        string? repositoryUrl,
        string? branch,
        string? gitUsername,
        string? gitAccessToken,
        CancellationToken cancellationToken)
    {
        var workspacePath = ResolveWorkspacePath(workspaceRoot, projectCode);
        Directory.CreateDirectory(Path.GetDirectoryName(workspacePath)!);

        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            Directory.CreateDirectory(workspacePath);
            return new WorkspacePreparationResult(
                true,
                workspacePath,
                RepositoryAvailable: false,
                RepositoryUrl: null,
                Branch: null,
                Commit: null,
                Dirty: false,
                Error: null);
        }

        var normalizedBranch = NormalizeOptional(branch);
        var authenticatedRepositoryUrl = BuildAuthenticatedUrl(repositoryUrl.Trim(), gitUsername, gitAccessToken);
        var secretValues = new[] { gitAccessToken, authenticatedRepositoryUrl }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        try
        {
            if (!Directory.Exists(Path.Combine(workspacePath, ".git")))
            {
                await CloneAsync(authenticatedRepositoryUrl, repositoryUrl.Trim(), normalizedBranch, workspacePath, secretValues, cancellationToken);
            }
            else
            {
                await EnsureRemoteUrlAsync(workspacePath, repositoryUrl.Trim(), cancellationToken);
                await EnsureRemoteUrlAsync(workspacePath, authenticatedRepositoryUrl, cancellationToken);
                try
                {
                    await RunGitOrThrowAsync("fetch --prune origin", workspacePath, secretValues, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(normalizedBranch))
                    {
                        await CheckoutBranchAsync(workspacePath, normalizedBranch, secretValues, cancellationToken);
                    }

                    await PullAsync(workspacePath, normalizedBranch, secretValues, cancellationToken);
                }
                finally
                {
                    await TryEnsureRemoteUrlAsync(workspacePath, repositoryUrl.Trim(), cancellationToken);
                }
            }

            var currentBranch = await ReadGitOutputAsync("rev-parse --abbrev-ref HEAD", workspacePath, secretValues, cancellationToken);
            var commit = await ReadGitOutputAsync("rev-parse HEAD", workspacePath, secretValues, cancellationToken);
            var status = await ReadGitOutputAsync("status --porcelain", workspacePath, secretValues, cancellationToken);
            return new WorkspacePreparationResult(
                true,
                workspacePath,
                RepositoryAvailable: true,
                RepositoryUrl: repositoryUrl,
                Branch: currentBranch,
                Commit: commit,
                Dirty: !string.IsNullOrWhiteSpace(status),
                Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new WorkspacePreparationResult(
                false,
                workspacePath,
                RepositoryAvailable: true,
                RepositoryUrl: repositoryUrl,
                Branch: normalizedBranch,
                Commit: null,
                Dirty: false,
                Error: SanitizeGitMessage(ex.Message, secretValues));
        }
    }

    internal static string ResolveWorkspacePath(string workspaceRoot, string? projectCode)
    {
        projectCode = string.IsNullOrWhiteSpace(projectCode) ? "_unscoped" : projectCode.Trim();
        var root = Path.GetFullPath(workspaceRoot);
        var path = Path.GetFullPath(Path.Combine(root, projectCode));
        var relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException("Project workspace path escapes WorkspaceRoot.");
        }

        return path;
    }

    private static async Task CloneAsync(
        string authenticatedRepositoryUrl,
        string repositoryUrl,
        string? branch,
        string workspacePath,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(workspacePath) && Directory.EnumerateFileSystemEntries(workspacePath).Any())
        {
            throw new InvalidOperationException("Workspace exists but is not a Git repository.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(workspacePath)!);
        var arguments = string.IsNullOrWhiteSpace(branch)
            ? $"clone {Quote(authenticatedRepositoryUrl)} {Quote(workspacePath)}"
            : $"clone --branch {Quote(branch)} {Quote(authenticatedRepositoryUrl)} {Quote(workspacePath)}";
        await RunGitOrThrowAsync(arguments, null, secretValues, cancellationToken);
        await TryEnsureRemoteUrlAsync(workspacePath, repositoryUrl, cancellationToken);
    }

    private static async Task EnsureRemoteUrlAsync(
        string workspacePath,
        string repositoryUrl,
        CancellationToken cancellationToken)
    {
        var currentUrl = await ReadGitOutputAsync("remote get-url origin", workspacePath, cancellationToken);
        if (!string.Equals(currentUrl, repositoryUrl, StringComparison.Ordinal))
        {
            await RunGitOrThrowAsync($"remote set-url origin {Quote(repositoryUrl)}", workspacePath, cancellationToken);
        }
    }

    private static async Task TryEnsureRemoteUrlAsync(
        string workspacePath,
        string repositoryUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureRemoteUrlAsync(workspacePath, repositoryUrl, cancellationToken);
        }
        catch
        {
            // Best-effort cleanup: the original Git failure is reported by the caller.
        }
    }

    private static async Task CheckoutBranchAsync(
        string workspacePath,
        string branch,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        var currentBranch = await ReadGitOutputAsync("rev-parse --abbrev-ref HEAD", workspacePath, secretValues, cancellationToken);
        if (string.Equals(currentBranch, branch, StringComparison.Ordinal))
        {
            return;
        }

        var checkout = await ProcessCommandRunner.RunAsync("git", $"checkout {Quote(branch)}", workspacePath, GitCommandTimeout, cancellationToken);
        if (checkout.Succeeded)
        {
            return;
        }

        await RunGitOrThrowAsync($"checkout -B {Quote(branch)} {Quote("origin/" + branch)}", workspacePath, secretValues, cancellationToken);
    }

    private static async Task PullAsync(
        string workspacePath,
        string? requestedBranch,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        var branch = await ReadGitOutputAsync("rev-parse --abbrev-ref HEAD", workspacePath, secretValues, cancellationToken);
        if (string.Equals(branch, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException("Current Git branch could not be resolved.");
        }

        var upstream = await ProcessCommandRunner.RunAsync("git", "rev-parse --abbrev-ref --symbolic-full-name @{u}", workspacePath, GitCommandTimeout, cancellationToken);
        if (upstream.Succeeded)
        {
            await RunGitOrThrowAsync("pull --ff-only", workspacePath, secretValues, cancellationToken);
            return;
        }

        await RunGitOrThrowAsync($"pull --ff-only origin {Quote(requestedBranch ?? branch)}", workspacePath, secretValues, cancellationToken);
    }

    private static async Task<string?> ReadGitOutputAsync(
        string arguments,
        string workspacePath,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        var result = await RunGitOrThrowAsync(arguments, workspacePath, secretValues, cancellationToken);
        return NormalizeOptional(result.Stdout);
    }

    private static Task<string?> ReadGitOutputAsync(
        string arguments,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        return ReadGitOutputAsync(arguments, workspacePath, Array.Empty<string>(), cancellationToken);
    }

    private static async Task<CommandProbeResult> RunGitOrThrowAsync(
        string arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        return await RunGitOrThrowAsync(arguments, workingDirectory, Array.Empty<string>(), cancellationToken);
    }

    private static async Task<CommandProbeResult> RunGitOrThrowAsync(
        string arguments,
        string? workingDirectory,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        var result = await ProcessCommandRunner.RunAsync("git", arguments, workingDirectory, GitCommandTimeout, cancellationToken);
        if (result.Succeeded)
        {
            return result;
        }

        var error = NormalizeOptional(result.Stderr) ?? NormalizeOptional(result.Stdout) ?? result.Error ?? "Git command failed.";
        error = SanitizeGitMessage(error, secretValues);
        throw new InvalidOperationException(error);
    }

    internal static string BuildAuthenticatedUrl(
        string repositoryUrl,
        string? gitUsername,
        string? gitAccessToken)
    {
        if (string.IsNullOrWhiteSpace(gitAccessToken))
        {
            return repositoryUrl;
        }

        var builder = new UriBuilder(repositoryUrl)
        {
            UserName = Uri.EscapeDataString(NormalizeOptional(gitUsername) ?? "oauth2"),
            Password = Uri.EscapeDataString(gitAccessToken.Trim())
        };
        return builder.Uri.ToString();
    }

    internal static string SanitizeGitMessage(string message, IReadOnlyCollection<string> secretValues)
    {
        foreach (var secret in secretValues)
        {
            if (!string.IsNullOrWhiteSpace(secret))
            {
                message = message.Replace(secret, "<redacted>", StringComparison.Ordinal);
            }
        }

        return message;
    }

    private static string? NormalizeOptional(string? value)
    {
        value = value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
