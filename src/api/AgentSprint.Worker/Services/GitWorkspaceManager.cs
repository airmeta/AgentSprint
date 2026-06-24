using System.Text.Json;

using AgentSprint.Worker.Models;

namespace AgentSprint.Worker.Services;

public sealed class GitWorkspaceManager
{
    private static readonly TimeSpan GitCommandTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int CloneMaxAttempts = 3;
    private const int AuditDiffMaxChars = 60000;

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
        string? gitCommitAuthorName,
        string? gitCommitAuthorEmail,
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
                await EnsureCommitAuthorAsync(workspacePath, gitCommitAuthorName, gitCommitAuthorEmail, secretValues, cancellationToken);
            }
            else
            {
                await EnsureRemoteUrlAsync(workspacePath, repositoryUrl.Trim(), cancellationToken);
                await EnsureCommitAuthorAsync(workspacePath, gitCommitAuthorName, gitCommitAuthorEmail, secretValues, cancellationToken);
                await EnsureRemoteUrlAsync(workspacePath, authenticatedRepositoryUrl, secretValues, cancellationToken);
                try
                {
                    await CleanWorkspaceAsync(workspacePath, secretValues, cancellationToken);
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

    /// <summary>
    /// <para>zh-cn:在 Codex 成功完成任务后发布工作区改动。该方法先把当前本地改动提交为任务提交，再拉取远端引用并合并远端目标分支，最后推送当前分支；如果合并出现冲突，会调用调用方提供的冲突修复回调，回调只负责编辑文件，不负责提交或推送。</para>
    /// <para>en-us:Publishes workspace changes after Codex finishes successfully. The method first commits the local task changes, then fetches remote refs, merges the remote target branch, and pushes the current branch; if the merge conflicts, it invokes the caller-provided resolver, which only edits files and does not commit or push.</para>
    /// </summary>
    /// <param name="workspacePath">
    /// <para>zh-cn:已经准备好的 Git 工作区路径。</para>
    /// <para>en-us:Prepared Git workspace path.</para>
    /// </param>
    /// <param name="repositoryUrl">
    /// <para>zh-cn:平台返回的真实仓库地址，用于发布期间临时恢复和清理 origin 地址。</para>
    /// <para>en-us:Real repository URL returned by the platform, used to restore and clean the origin URL during publishing.</para>
    /// </param>
    /// <param name="gitUsername">
    /// <para>zh-cn:可选 Git 用户名，存在访问令牌时写入临时认证 URL。</para>
    /// <para>en-us:Optional Git username, inserted into the temporary authenticated URL when an access token is available.</para>
    /// </param>
    /// <param name="gitAccessToken">
    /// <para>zh-cn:可选 Git 访问令牌，仅用于本次 fetch/push 进程参数并在日志中脱敏。</para>
    /// <para>en-us:Optional Git access token, used only for this fetch/push process invocation and redacted in logs.</para>
    /// </param>
    /// <param name="commitMessage">
    /// <para>zh-cn:任务本地改动提交信息；换行会被压缩为空格。</para>
    /// <para>en-us:Commit message for the local task changes; newlines are collapsed into spaces.</para>
    /// </param>
    /// <param name="conflictResolver">
    /// <para>zh-cn:合并冲突修复回调。回调返回成功后，本方法会检查冲突标记、暂存改动、完成合并提交并推送。</para>
    /// <para>en-us:Merge-conflict resolver callback. After it succeeds, this method checks conflict markers, stages changes, completes the merge commit, and pushes.</para>
    /// </param>
    /// <param name="cancellationToken">
    /// <para>zh-cn:取消令牌。</para>
    /// <para>en-us:Cancellation token.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:发布是否成功、是否有变更、是否已推送、冲突是否经 Codex 修复、分支、提交号和错误摘要。</para>
    /// <para>en-us:Whether publishing succeeded, whether changes existed, whether a push occurred, whether Codex resolved conflicts, the branch, commit, and error summary.</para>
    /// </returns>
    public async Task<WorkspacePublishResult> PublishAsync(
        string workspacePath,
        string repositoryUrl,
        string? gitUsername,
        string? gitAccessToken,
        string? gitCommitAuthorName,
        string? gitCommitAuthorEmail,
        string commitMessage,
        Func<GitConflictResolutionRequest, CancellationToken, Task<GitConflictResolutionResult>> conflictResolver,
        CancellationToken cancellationToken,
        string? baselineCommit = null)
    {
        var authenticatedRepositoryUrl = BuildAuthenticatedUrl(repositoryUrl.Trim(), gitUsername, gitAccessToken);
        var secretValues = new[] { gitAccessToken, authenticatedRepositoryUrl }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var currentBranch = string.Empty;

        try
        {
            if (!Directory.Exists(Path.Combine(workspacePath, ".git")))
            {
                throw new InvalidOperationException("Workspace is not a Git repository.");
            }

            currentBranch = await ReadGitOutputAsync("rev-parse --abbrev-ref HEAD", workspacePath, secretValues, cancellationToken) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentBranch) || string.Equals(currentBranch, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Current Git branch could not be resolved.");
            }

            var baseCommit = NormalizeOptional(baselineCommit) ??
                await ReadGitOutputAsync("rev-parse HEAD", workspacePath, secretValues, cancellationToken);
            var status = await ReadGitOutputAsync("status --porcelain", workspacePath, secretValues, cancellationToken);
            var changedFilesJson = BuildChangedFilesJson(status);
            if (string.IsNullOrWhiteSpace(status))
            {
                return new WorkspacePublishResult(
                    true,
                    workspacePath,
                    HasChanges: false,
                    Pushed: false,
                    ConflictResolved: false,
                    Branch: currentBranch,
                    Commit: baseCommit,
                    ChangedFilesJson: changedFilesJson,
                    Error: null);
            }

            await EnsureRemoteUrlAsync(workspacePath, authenticatedRepositoryUrl, secretValues, cancellationToken);
            await EnsureCommitAuthorAsync(workspacePath, gitCommitAuthorName, gitCommitAuthorEmail, secretValues, cancellationToken);
            try
            {
                await RunGitOrThrowAsync("add -A", workspacePath, secretValues, cancellationToken);
                await CommitStagedChangesAsync(workspacePath, commitMessage, secretValues, cancellationToken);
                await RunGitOrThrowAsync("fetch --prune origin", workspacePath, secretValues, cancellationToken);

                var conflictResolved = false;
                var merge = await ProcessCommandRunner.RunAsync(
                    "git",
                    $"merge --no-edit {Quote("origin/" + currentBranch)}",
                    workspacePath,
                    GitCommandTimeout,
                    secretValues,
                    cancellationToken);
                if (!merge.Succeeded)
                {
                    var conflictFiles = await ReadConflictFilesAsync(workspacePath, secretValues, cancellationToken);
                    if (conflictFiles.Count == 0)
                    {
                        throw new InvalidOperationException(SanitizeGitFailure(merge, secretValues));
                    }

                    var resolverResult = await conflictResolver(
                        new GitConflictResolutionRequest(
                            workspacePath,
                            currentBranch,
                            conflictFiles,
                            "merge",
                            SanitizeGitFailure(merge, secretValues)),
                        cancellationToken);
                    if (!resolverResult.Succeeded)
                    {
                        throw new InvalidOperationException(resolverResult.Error ?? "Codex conflict resolution failed.");
                    }

                    await RunGitOrThrowAsync("diff --check", workspacePath, secretValues, cancellationToken);
                    await RunGitOrThrowAsync("add -A", workspacePath, secretValues, cancellationToken);
                    await RunGitOrThrowAsync("commit --no-edit", workspacePath, secretValues, cancellationToken);
                    conflictResolved = true;
                }

                await RunGitOrThrowAsync($"push origin {Quote(currentBranch)}", workspacePath, secretValues, cancellationToken);
                var pushedCommit = await ReadGitOutputAsync("rev-parse HEAD", workspacePath, secretValues, cancellationToken);
                var finalChangedFilesJson = await BuildChangedFilesJsonAsync(
                    workspacePath,
                    baseCommit,
                    pushedCommit,
                    changedFilesJson,
                    secretValues,
                    cancellationToken);
                return new WorkspacePublishResult(
                    true,
                    workspacePath,
                    HasChanges: true,
                    Pushed: true,
                    ConflictResolved: conflictResolved,
                    Branch: currentBranch,
                    Commit: pushedCommit,
                    ChangedFilesJson: finalChangedFilesJson,
                    Error: null);
            }
            finally
            {
                await TryEnsureRemoteUrlAsync(workspacePath, repositoryUrl.Trim(), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new WorkspacePublishResult(
                false,
                workspacePath,
                HasChanges: true,
                Pushed: false,
                ConflictResolved: false,
                Branch: NormalizeOptional(currentBranch),
                Commit: null,
                ChangedFilesJson: null,
                Error: SanitizeGitMessage(ex.Message, secretValues));
        }
    }

    public static async Task<CodeAuditGitContextResult> BuildCodeAuditContextAsync(
        string workspacePath,
        string? sourceCommitId,
        string? fallbackChangedFilesJson,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var currentBranch = await TryReadGitOutputAsync("rev-parse --abbrev-ref HEAD", workspacePath, cancellationToken);
        var currentHead = await TryReadGitOutputAsync("rev-parse HEAD", workspacePath, cancellationToken);
        var normalizedSourceCommit = NormalizeOptional(sourceCommitId);
        var headCommit = normalizedSourceCommit ?? currentHead;
        string? baseCommit = null;
        bool? sourceCommitReachable = null;
        bool? sourceCommitBehindHead = null;

        if (!string.IsNullOrWhiteSpace(normalizedSourceCommit))
        {
            if (!string.IsNullOrWhiteSpace(currentBranch) && !string.Equals(currentBranch, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                await TryRunGitAsync($"fetch --deepen=50 origin {Quote(currentBranch)}", workspacePath, cancellationToken);
            }

            var resolvedSource = await TryReadGitOutputAsync($"rev-parse {Quote(normalizedSourceCommit)}", workspacePath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(resolvedSource))
            {
                headCommit = resolvedSource;
                baseCommit = await TryReadGitOutputAsync($"rev-parse {Quote(resolvedSource + "^")}", workspacePath, cancellationToken);
                if (string.IsNullOrWhiteSpace(baseCommit))
                {
                    warnings.Add("Unable to resolve the parent commit for the source commit; diff boundary is incomplete.");
                }

                sourceCommitReachable = !string.IsNullOrWhiteSpace(currentHead)
                    ? await IsGitCommandSuccessfulAsync($"merge-base --is-ancestor {Quote(resolvedSource)} {Quote(currentHead)}", workspacePath, cancellationToken)
                    : null;
                sourceCommitBehindHead = !string.IsNullOrWhiteSpace(currentHead) &&
                    !string.Equals(resolvedSource, currentHead, StringComparison.Ordinal);
            }
            else
            {
                warnings.Add("Source commit could not be resolved in the prepared workspace; falling back to current branch head context.");
                headCommit = currentHead;
            }
        }
        else
        {
            warnings.Add("Source commit is not available; changed file coverage must be confirmed manually.");
        }

        string? nameStatus = null;
        string? diffStat = null;
        string? diff = null;
        if (!string.IsNullOrWhiteSpace(baseCommit) && !string.IsNullOrWhiteSpace(headCommit))
        {
            var range = $"{Quote(baseCommit)}..{Quote(headCommit)}";
            nameStatus = await TryReadGitOutputAsync($"diff --name-status {range}", workspacePath, cancellationToken);
            diffStat = await TryReadGitOutputAsync($"diff --stat {range}", workspacePath, cancellationToken);
            diff = await TryReadGitOutputAsync($"diff --no-ext-diff --unified=80 {range}", workspacePath, cancellationToken);
        }

        var changedFilesJson = !string.IsNullOrWhiteSpace(nameStatus)
            ? BuildChangedFilesJsonFromNameStatus(nameStatus)
            : NormalizeOptional(fallbackChangedFilesJson);
        if (string.IsNullOrWhiteSpace(changedFilesJson))
        {
            changedFilesJson = "[]";
        }

        if (string.Equals(changedFilesJson, "[]", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(fallbackChangedFilesJson))
        {
            changedFilesJson = fallbackChangedFilesJson;
        }

        var codeContext = BuildAuditCodeContext(currentBranch, baseCommit, headCommit, currentHead, nameStatus, diffStat);
        var limitedDiff = LimitText(diff, AuditDiffMaxChars, warnings);
        return new CodeAuditGitContextResult(
            currentBranch,
            baseCommit,
            headCommit,
            currentHead,
            changedFilesJson,
            limitedDiff,
            codeContext,
            sourceCommitReachable,
            sourceCommitBehindHead,
            warnings.Count == 0 ? null : string.Join(Environment.NewLine, warnings.Distinct(StringComparer.Ordinal)));
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
            ? $"clone --depth 1 {Quote(authenticatedRepositoryUrl)} {Quote(workspacePath)}"
            : $"clone --depth 1 --single-branch --branch {Quote(branch)} {Quote(authenticatedRepositoryUrl)} {Quote(workspacePath)}";

        for (var attempt = 1; attempt <= CloneMaxAttempts; attempt++)
        {
            var result = await ProcessCommandRunner.RunAsync(
                "git",
                arguments,
                null,
                GitCommandTimeout,
                secretValues,
                cancellationToken);
            if (result.Succeeded)
            {
                await TryEnsureRemoteUrlAsync(workspacePath, repositoryUrl, cancellationToken);
                return;
            }

            var error = SanitizeGitFailure(result, secretValues);
            if (attempt >= CloneMaxAttempts || !IsRetryableCloneFailure(result, error))
            {
                throw new InvalidOperationException(error);
            }

            WorkerDiagnostics.Warn(
                "Git clone失败，准备重试",
                $"attempt={attempt}, maxAttempts={CloneMaxAttempts}, workspacePath={workspacePath}, error={WorkerDiagnostics.Trim(error, 1000)}");
            DeleteDirectoryIfExists(workspacePath);
            await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
        }

        await TryEnsureRemoteUrlAsync(workspacePath, repositoryUrl, cancellationToken);
    }

    private static bool IsRetryableCloneFailure(CommandProbeResult result, string error)
    {
        if (result.TimedOut)
        {
            return true;
        }

        return error.Contains("early EOF", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("the remote end hung up unexpectedly", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("connection timed out", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("connection reset", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("failed to connect", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("unable to access", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("TLS connection", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("RPC failed", StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }

    private static async Task EnsureRemoteUrlAsync(
        string workspacePath,
        string repositoryUrl,
        CancellationToken cancellationToken)
    {
        await EnsureRemoteUrlAsync(workspacePath, repositoryUrl, Array.Empty<string>(), cancellationToken);
    }

    private static async Task EnsureRemoteUrlAsync(
        string workspacePath,
        string repositoryUrl,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        var currentUrl = await ReadGitOutputAsync("remote get-url origin", workspacePath, secretValues, cancellationToken);
        if (!string.Equals(currentUrl, repositoryUrl, StringComparison.Ordinal))
        {
            await RunGitOrThrowAsync($"remote set-url origin {Quote(repositoryUrl)}", workspacePath, secretValues, cancellationToken);
        }
    }

    private static async Task EnsureCommitAuthorAsync(
        string workspacePath,
        string? commitAuthorName,
        string? commitAuthorEmail,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        commitAuthorName = NormalizeOptional(commitAuthorName);
        commitAuthorEmail = NormalizeOptional(commitAuthorEmail);
        if (commitAuthorName is null && commitAuthorEmail is null)
        {
            return;
        }

        if (commitAuthorName is null || commitAuthorEmail is null)
        {
            throw new InvalidOperationException("Git commit author name and email must be configured together.");
        }

        await RunGitOrThrowAsync($"config user.name {Quote(commitAuthorName)}", workspacePath, secretValues, cancellationToken);
        await RunGitOrThrowAsync($"config user.email {Quote(commitAuthorEmail)}", workspacePath, secretValues, cancellationToken);
    }

    private static async Task CleanWorkspaceAsync(
        string workspacePath,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        await RunGitOrThrowAsync("reset --hard", workspacePath, secretValues, cancellationToken);
        await RunGitOrThrowAsync("clean -fd", workspacePath, secretValues, cancellationToken);
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

        var checkout = await ProcessCommandRunner.RunAsync(
            "git",
            $"checkout {Quote(branch)}",
            workspacePath,
            GitCommandTimeout,
            secretValues,
            cancellationToken);
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

        var upstream = await ProcessCommandRunner.RunAsync(
            "git",
            "rev-parse --abbrev-ref --symbolic-full-name @{u}",
            workspacePath,
            GitCommandTimeout,
            secretValues,
            cancellationToken);
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

    private static async Task<IReadOnlyList<string>> ReadConflictFilesAsync(
        string workspacePath,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        var output = await ReadGitOutputAsync("diff --name-only --diff-filter=U", workspacePath, secretValues, cancellationToken);
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static Task<string?> ReadGitOutputAsync(
        string arguments,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        return ReadGitOutputAsync(arguments, workspacePath, Array.Empty<string>(), cancellationToken);
    }

    private static async Task<string?> TryReadGitOutputAsync(
        string arguments,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var result = await ProcessCommandRunner.RunAsync(
            "git",
            arguments,
            workspacePath,
            GitCommandTimeout,
            cancellationToken);
        return result.Succeeded ? NormalizeOptional(result.Stdout) : null;
    }

    private static async Task TryRunGitAsync(
        string arguments,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        _ = await ProcessCommandRunner.RunAsync(
            "git",
            arguments,
            workspacePath,
            GitCommandTimeout,
            cancellationToken);
    }

    private static async Task<bool> IsGitCommandSuccessfulAsync(
        string arguments,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var result = await ProcessCommandRunner.RunAsync(
            "git",
            arguments,
            workspacePath,
            GitCommandTimeout,
            cancellationToken);
        return result.Succeeded;
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
        var result = await ProcessCommandRunner.RunAsync(
            "git",
            arguments,
            workingDirectory,
            GitCommandTimeout,
            secretValues,
            cancellationToken);
        if (result.Succeeded)
        {
            return result;
        }

        var error = NormalizeOptional(result.Stderr) ?? NormalizeOptional(result.Stdout) ?? result.Error ?? "Git command failed.";
        error = SanitizeGitMessage(error, secretValues);
        throw new InvalidOperationException(error);
    }

    internal static string BuildChangedFilesJson(string? porcelainStatus)
    {
        var changes = ParseChangedFiles(porcelainStatus)
            .OrderBy(change => change.Path, StringComparer.Ordinal)
            .ToArray();
        return JsonSerializer.Serialize(changes, JsonOptions);
    }

    internal static string BuildChangedFilesJsonFromNameStatus(string? nameStatus)
    {
        var changes = ParseNameStatusChangedFiles(nameStatus)
            .OrderBy(change => change.Path, StringComparer.Ordinal)
            .ToArray();
        return JsonSerializer.Serialize(changes, JsonOptions);
    }

    private static async Task<string> BuildChangedFilesJsonAsync(
        string workspacePath,
        string? baseCommit,
        string? headCommit,
        string fallbackChangedFilesJson,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baseCommit) ||
            string.IsNullOrWhiteSpace(headCommit) ||
            string.Equals(baseCommit, headCommit, StringComparison.Ordinal))
        {
            return fallbackChangedFilesJson;
        }

        var nameStatus = await ReadGitOutputAsync(
            $"diff --name-status {Quote(baseCommit)}..{Quote(headCommit)}",
            workspacePath,
            secretValues,
            cancellationToken);
        var changedFilesJson = BuildChangedFilesJsonFromNameStatus(nameStatus);
        return string.Equals(changedFilesJson, "[]", StringComparison.Ordinal)
            ? fallbackChangedFilesJson
            : changedFilesJson;
    }

    private static IEnumerable<GitChangedFile> ParseChangedFiles(string? porcelainStatus)
    {
        if (string.IsNullOrWhiteSpace(porcelainStatus))
        {
            yield break;
        }

        foreach (var rawLine in porcelainStatus.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd();
            if (line.Length < 3)
            {
                continue;
            }

            string code;
            string pathText;
            if (line.Length >= 4 && line[2] == ' ')
            {
                code = line[..2].Trim();
                pathText = line[3..].Trim();
            }
            else
            {
                code = line[..1].Trim();
                pathText = line[2..].Trim();
            }

            if (string.IsNullOrWhiteSpace(pathText))
            {
                continue;
            }

            var path = pathText;
            string? oldPath = null;
            var renameSeparator = pathText.IndexOf(" -> ", StringComparison.Ordinal);
            if (renameSeparator >= 0)
            {
                oldPath = pathText[..renameSeparator].Trim();
                path = pathText[(renameSeparator + 4)..].Trim();
            }

            yield return new GitChangedFile(path, ResolveGitChangeStatus(code), oldPath);
        }
    }

    private static IEnumerable<GitChangedFile> ParseNameStatusChangedFiles(string? nameStatus)
    {
        if (string.IsNullOrWhiteSpace(nameStatus))
        {
            yield break;
        }

        foreach (var rawLine in nameStatus.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var columns = rawLine.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (columns.Length < 2)
            {
                continue;
            }

            var code = columns[0];
            if (code.StartsWith('R') || code.StartsWith('C'))
            {
                if (columns.Length < 3)
                {
                    continue;
                }

                yield return new GitChangedFile(columns[2], ResolveGitChangeStatus(code), columns[1]);
                continue;
            }

            yield return new GitChangedFile(columns[1], ResolveGitChangeStatus(code), null);
        }
    }

    private static string ResolveGitChangeStatus(string code)
    {
        if (code.Contains('R', StringComparison.Ordinal))
        {
            return "renamed";
        }

        if (code.Contains('C', StringComparison.Ordinal))
        {
            return "copied";
        }

        if (code.Contains('D', StringComparison.Ordinal))
        {
            return "deleted";
        }

        if (code.Contains('A', StringComparison.Ordinal) || code == "??")
        {
            return "added";
        }

        if (code.Contains('M', StringComparison.Ordinal))
        {
            return "modified";
        }

        return "changed";
    }

    private static async Task CommitStagedChangesAsync(
        string workspacePath,
        string message,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        var messagePath = Path.Combine(Path.GetTempPath(), $"agentsprint-commit-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(messagePath, NormalizeCommitMessage(message), cancellationToken);
            await RunGitOrThrowAsync($"commit -F {Quote(messagePath)}", workspacePath, secretValues, cancellationToken);
        }
        finally
        {
            try
            {
                File.Delete(messagePath);
            }
            catch
            {
                // Best-effort cleanup for a temporary commit message file.
            }
        }
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

    private static string SanitizeGitFailure(CommandProbeResult result, IReadOnlyCollection<string> secretValues)
    {
        var error = NormalizeOptional(result.Stderr) ?? NormalizeOptional(result.Stdout) ?? result.Error ?? "Git command failed.";
        return SanitizeGitMessage(error, secretValues);
    }

    private static string NormalizeCommitMessage(string message)
    {
        var lines = message
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length == 0)
        {
            return "AgentSprint worker task update";
        }

        if (lines.Length == 1)
        {
            return lines[0];
        }

        return lines[0] + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines.Skip(1));
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

    private static string BuildAuditCodeContext(
        string? branch,
        string? baseCommit,
        string? headCommit,
        string? currentHead,
        string? nameStatus,
        string? diffStat)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Git audit boundary:");
        builder.AppendLine($"- Branch: {branch ?? string.Empty}");
        builder.AppendLine($"- Base commit: {baseCommit ?? string.Empty}");
        builder.AppendLine($"- Head/source commit: {headCommit ?? string.Empty}");
        builder.AppendLine($"- Current branch head: {currentHead ?? string.Empty}");
        if (!string.IsNullOrWhiteSpace(diffStat))
        {
            builder.AppendLine();
            builder.AppendLine("Diff stat:");
            builder.AppendLine(diffStat.Trim());
        }

        if (!string.IsNullOrWhiteSpace(nameStatus))
        {
            builder.AppendLine();
            builder.AppendLine("Changed file name-status:");
            builder.AppendLine(nameStatus.Trim());
        }

        return builder.ToString().Trim();
    }

    private static string? LimitText(string? value, int maxChars, ICollection<string> warnings)
    {
        value = NormalizeOptional(value);
        if (value is null || value.Length <= maxChars)
        {
            return value;
        }

        warnings.Add($"Diff was truncated to {maxChars} characters for prompt safety; inspect the repository directly for omitted hunks.");
        return value[..maxChars] + Environment.NewLine + $"[diff truncated at {maxChars} characters]";
    }

    private sealed record GitChangedFile(string Path, string Status, string? OldPath);
}

public sealed record CodeAuditGitContextResult(
    string? Branch,
    string? BaseCommitId,
    string? HeadCommitId,
    string? CurrentBranchHeadCommitId,
    string ChangedFilesJson,
    string? Diff,
    string? CodeContext,
    bool? SourceCommitReachable,
    bool? SourceCommitBehindHead,
    string? Warning);
