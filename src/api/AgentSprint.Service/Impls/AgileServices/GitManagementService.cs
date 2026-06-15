using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.AgileServices;

namespace AgentSprint.Service.Impls.AgileServices;

public sealed class GitManagementService : AgentSprintServiceBase, IGitManagementService
{
    private const int PushRecordLimit = 50;

    private readonly IGitAccountDomain _accountDomain;
    private readonly IGitRepositoryDomain _repositoryDomain;
    private readonly IGitBranchOperationDomain _operationDomain;
    private readonly IGitCommandRunner _gitCommandRunner;

    /// <summary>
    /// zh-cn: 创建 Git 管理服务，负责维护 Git 账户、仓库配置，并通过 Git runner 执行分支创建、备份删除和推送记录读取。
    /// en-us: Creates the Git management service, maintaining Git accounts and repositories while using a Git runner to create branches, delete branches with backups, and read push records.
    /// </summary>
    public GitManagementService(
        IGitAccountDomain accountDomain,
        IGitRepositoryDomain repositoryDomain,
        IGitBranchOperationDomain operationDomain,
        IGitCommandRunner gitCommandRunner)
    {
        _accountDomain = accountDomain;
        _repositoryDomain = repositoryDomain;
        _operationDomain = operationDomain;
        _gitCommandRunner = gitCommandRunner;
    }

    /// <inheritdoc />
    public async Task<GitAccountResult> CreateAccountAsync(SaveGitAccountRequest request, string userId)
    {
        var code = NormalizeRequired(request.Code, "Git account code is required.");
        if ((await _accountDomain.ListAsync(entity => entity.Code == code)).Count > 0)
        {
            throw new InvalidOperationException("Git account code already exists.");
        }

        var entity = new GitAccountEntity
        {
            Code = code,
            Name = NormalizeRequired(request.Name, "Git account name is required."),
            Username = NormalizeRequired(request.Username, "Git username is required."),
            AccessToken = NormalizeOptional(request.AccessToken),
            Description = NormalizeOptional(request.Description),
            Status = NormalizeAccountStatus(request.Status, GitAccountStatuses.Active),
            CreatedBy = userId
        };

        await _accountDomain.CreateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<GitAccountResult> UpdateAccountAsync(string id, SaveGitAccountRequest request)
    {
        var entity = await GetAccountOrThrowAsync(id);
        entity.Name = NormalizeRequired(request.Name, "Git account name is required.");
        entity.Username = NormalizeRequired(request.Username, "Git username is required.");
        entity.AccessToken = NormalizeOptional(request.AccessToken);
        entity.Description = NormalizeOptional(request.Description);
        entity.Status = NormalizeAccountStatus(request.Status, entity.Status);
        await _accountDomain.UpdateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitAccountResult>> ListAccountsAsync(string? keyword = null, string? status = null)
    {
        var normalizedKeyword = NormalizeOptional(keyword);
        var normalizedStatus = NormalizeOptional(status);
        var entities = await _accountDomain.ListAsync(entity =>
            (normalizedStatus == null || entity.Status == normalizedStatus) &&
            (normalizedKeyword == null ||
                entity.Code.Contains(normalizedKeyword) ||
                entity.Name.Contains(normalizedKeyword) ||
                entity.Username.Contains(normalizedKeyword)));

        return entities
            .OrderByDescending(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<GitRepositoryResult> CreateRepositoryAsync(SaveGitRepositoryRequest request, string userId)
    {
        var code = NormalizeRequired(request.Code, "Git repository code is required.");
        if ((await _repositoryDomain.ListAsync(entity => entity.Code == code)).Count > 0)
        {
            throw new InvalidOperationException("Git repository code already exists.");
        }

        var accountId = NormalizeOptional(request.GitAccountId);
        if (accountId is not null)
        {
            await GetAccountOrThrowAsync(accountId);
        }

        var entity = new GitRepositoryEntity
        {
            Code = code,
            Name = NormalizeRequired(request.Name, "Git repository name is required."),
            RepositoryUrl = NormalizeRepositoryUrl(request.RepositoryUrl),
            DefaultBranch = NormalizeOptional(request.DefaultBranch) ?? "main",
            GitAccountId = accountId,
            LocalPath = NormalizeOptional(request.LocalPath),
            Description = NormalizeOptional(request.Description),
            Status = NormalizeRepositoryStatus(request.Status, GitRepositoryStatuses.Active),
            CreatedBy = userId
        };

        await _repositoryDomain.CreateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<GitRepositoryResult> UpdateRepositoryAsync(string id, SaveGitRepositoryRequest request)
    {
        var entity = await GetRepositoryOrThrowAsync(id);
        var accountId = NormalizeOptional(request.GitAccountId);
        if (accountId is not null)
        {
            await GetAccountOrThrowAsync(accountId);
        }

        entity.Name = NormalizeRequired(request.Name, "Git repository name is required.");
        entity.RepositoryUrl = NormalizeRepositoryUrl(request.RepositoryUrl);
        entity.DefaultBranch = NormalizeOptional(request.DefaultBranch) ?? entity.DefaultBranch;
        entity.GitAccountId = accountId;
        entity.LocalPath = NormalizeOptional(request.LocalPath);
        entity.Description = NormalizeOptional(request.Description);
        entity.Status = NormalizeRepositoryStatus(request.Status, entity.Status);
        await _repositoryDomain.UpdateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitRepositoryResult>> ListRepositoriesAsync(
        string? keyword = null,
        string? status = null,
        string? gitAccountId = null)
    {
        var normalizedKeyword = NormalizeOptional(keyword);
        var normalizedStatus = NormalizeOptional(status);
        var normalizedAccountId = NormalizeOptional(gitAccountId);
        var entities = await _repositoryDomain.ListAsync(entity =>
            (normalizedStatus == null || entity.Status == normalizedStatus) &&
            (normalizedAccountId == null || entity.GitAccountId == normalizedAccountId) &&
            (normalizedKeyword == null ||
                entity.Code.Contains(normalizedKeyword) ||
                entity.Name.Contains(normalizedKeyword) ||
                entity.RepositoryUrl.Contains(normalizedKeyword)));

        return entities
            .OrderByDescending(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<GitBranchOperationResult> CreateBranchAsync(
        string repositoryId,
        CreateGitBranchRequest request,
        string userId)
    {
        var repository = await GetRepositoryOrThrowAsync(repositoryId);
        var branchName = NormalizeBranchName(request.BranchName, "Branch name is required.");
        var sourceBranch = NormalizeOptional(request.SourceBranch) ?? repository.DefaultBranch ?? "main";

        try
        {
            var workspace = await PrepareWorkspaceAsync(repository);
            await RunGitOrThrowAsync(workspace, ["fetch", "origin", "--prune"]);
            await RunGitOrThrowAsync(workspace, ["switch", sourceBranch]);
            await RunGitOrThrowAsync(workspace, ["pull", "--ff-only", "origin", sourceBranch]);
            await RunGitOrThrowAsync(workspace, ["switch", "-c", branchName]);
            await RunGitOrThrowAsync(workspace, ["push", "-u", "origin", branchName]);
            return await SaveOperationAsync(repository, GitBranchOperationTypes.CreateBranch, branchName, sourceBranch, null, null, null, null, GitBranchOperationStatuses.Success, "Branch created.", userId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return await SaveOperationAsync(repository, GitBranchOperationTypes.CreateBranch, branchName, sourceBranch, null, null, null, null, GitBranchOperationStatuses.Failed, ex.Message, userId);
        }
    }

    /// <inheritdoc />
    public async Task<GitBranchOperationResult> DeleteBranchAsync(
        string repositoryId,
        DeleteGitBranchRequest request,
        string userId)
    {
        var repository = await GetRepositoryOrThrowAsync(repositoryId);
        var branchName = NormalizeBranchName(request.BranchName, "Branch name is required.");
        var backupBranch = NormalizeOptional(request.BackupBranch) ??
            $"backup/{branchName.Replace('/', '-')}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        try
        {
            var workspace = await PrepareWorkspaceAsync(repository);
            await RunGitOrThrowAsync(workspace, ["fetch", "origin", "--prune"]);
            await RunGitOrThrowAsync(workspace, ["switch", "-c", backupBranch, $"origin/{branchName}"]);
            await RunGitOrThrowAsync(workspace, ["push", "-u", "origin", backupBranch]);
            await RunGitOrThrowAsync(workspace, ["push", "origin", "--delete", branchName]);
            await RunGitOrThrowAsync(workspace, ["branch", "-D", branchName]);
            return await SaveOperationAsync(repository, GitBranchOperationTypes.DeleteBranch, branchName, null, backupBranch, null, null, null, GitBranchOperationStatuses.Success, "Branch backed up and deleted.", userId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return await SaveOperationAsync(repository, GitBranchOperationTypes.DeleteBranch, branchName, null, backupBranch, null, null, null, GitBranchOperationStatuses.Failed, ex.Message, userId);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitBranchOperationResult>> ReadBranchPushRecordsAsync(
        string repositoryId,
        string? branch,
        string userId)
    {
        var repository = await GetRepositoryOrThrowAsync(repositoryId);
        var normalizedBranch = NormalizeOptional(branch);
        var workspace = await PrepareWorkspaceAsync(repository);
        await RunGitOrThrowAsync(workspace, ["fetch", "origin", "--prune"]);

        var revision = normalizedBranch is null ? "--remotes=origin" : $"origin/{NormalizeBranchName(normalizedBranch, "Branch name is required.")}";
        var result = await RunGitOrThrowAsync(workspace, [
            "log",
            revision,
            $"--max-count={PushRecordLimit}",
            "--date=iso-strict",
            "--pretty=format:%H%x1f%ad%x1f%s%x1e"
        ]);

        var records = ParseLog(repository, normalizedBranch ?? "all", result.StandardOutput, userId);
        foreach (var record in records)
        {
            await _operationDomain.CreateAsync(record);
        }

        return records.Select(ToResult).ToList();
    }

    private async Task<string> PrepareWorkspaceAsync(GitRepositoryEntity repository)
    {
        var workspace = NormalizeOptional(repository.LocalPath) ??
            Path.Combine(Path.GetTempPath(), "agentsprint-git", Hash(repository.RepositoryUrl));
        var gitDirectory = Path.Combine(workspace, ".git");
        if (Directory.Exists(gitDirectory))
        {
            return workspace;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(workspace)!);
        await RunGitOrThrowAsync(
            Path.GetDirectoryName(workspace)!,
            ["clone", await BuildAuthenticatedUrlAsync(repository), workspace]);
        return workspace;
    }

    private async Task<GitCommandResult> RunGitOrThrowAsync(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var result = await _gitCommandRunner.RunAsync(workingDirectory, arguments);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? "Git command failed."
                    : result.StandardError.Trim());
        }

        return result;
    }

    private async Task<string> BuildAuthenticatedUrlAsync(GitRepositoryEntity repository)
    {
        if (string.IsNullOrWhiteSpace(repository.GitAccountId))
        {
            return repository.RepositoryUrl;
        }

        var account = await _accountDomain.GetAsync(repository.GitAccountId);
        if (account is null || string.IsNullOrWhiteSpace(account.AccessToken))
        {
            return repository.RepositoryUrl;
        }

        var uri = new UriBuilder(repository.RepositoryUrl)
        {
            UserName = Uri.EscapeDataString(account.Username),
            Password = Uri.EscapeDataString(account.AccessToken)
        };
        return uri.Uri.ToString();
    }

    private async Task<GitBranchOperationResult> SaveOperationAsync(
        GitRepositoryEntity repository,
        string operationType,
        string branchName,
        string? sourceBranch,
        string? backupBranch,
        string? commitHash,
        string? commitMessage,
        DateTime? pushedAt,
        string status,
        string? message,
        string userId)
    {
        var entity = new GitBranchOperationEntity
        {
            RepositoryId = repository.Id,
            AccountId = repository.GitAccountId,
            OperationType = operationType,
            BranchName = branchName,
            SourceBranch = sourceBranch,
            BackupBranch = backupBranch,
            CommitHash = commitHash,
            CommitMessage = commitMessage,
            PushedAt = pushedAt,
            Status = status,
            Message = message,
            CreatedBy = userId
        };
        await _operationDomain.CreateAsync(entity);
        return ToResult(entity);
    }

    private IEnumerable<GitBranchOperationEntity> ParseLog(
        GitRepositoryEntity repository,
        string branch,
        string output,
        string userId)
    {
        return output
            .Split('\u001e', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('\u001f'))
            .Where(parts => parts.Length >= 3)
            .Select(parts => new GitBranchOperationEntity
            {
                RepositoryId = repository.Id,
                AccountId = repository.GitAccountId,
                OperationType = GitBranchOperationTypes.PushRecord,
                BranchName = branch,
                CommitHash = parts[0],
                PushedAt = DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var pushedAt)
                    ? pushedAt
                    : null,
                CommitMessage = parts[2],
                Status = GitBranchOperationStatuses.Success,
                Message = "Push record read.",
                CreatedBy = userId
            })
            .ToList();
    }

    private async Task<GitAccountEntity> GetAccountOrThrowAsync(string id)
    {
        return await _accountDomain.GetAsync(id) ?? throw new InvalidOperationException("Git account does not exist.");
    }

    private async Task<GitRepositoryEntity> GetRepositoryOrThrowAsync(string id)
    {
        return await _repositoryDomain.GetAsync(id) ?? throw new InvalidOperationException("Git repository does not exist.");
    }

    private static string NormalizeRepositoryUrl(string? repositoryUrl)
    {
        var normalized = NormalizeRequired(repositoryUrl, "Git repository URL is required.");
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Git repository URL must be an http or https URL.");
        }

        return normalized;
    }

    private static string NormalizeBranchName(string? branchName, string message)
    {
        var normalized = NormalizeRequired(branchName, message);
        if (normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.EndsWith("/", StringComparison.Ordinal) ||
            normalized.Contains(" ", StringComparison.Ordinal) ||
            normalized.Contains("\\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Branch name is invalid.");
        }

        return normalized;
    }

    private static string NormalizeAccountStatus(string? status, string currentStatus)
    {
        var normalized = NormalizeOptional(status) ?? currentStatus;
        return normalized switch
        {
            GitAccountStatuses.Active or GitAccountStatuses.Disabled => normalized,
            _ => currentStatus
        };
    }

    private static string NormalizeRepositoryStatus(string? status, string currentStatus)
    {
        var normalized = NormalizeOptional(status) ?? currentStatus;
        return normalized switch
        {
            GitRepositoryStatuses.Active or GitRepositoryStatuses.Disabled => normalized,
            _ => currentStatus
        };
    }

    private static string NormalizeRequired(string? value, string message)
    {
        var normalized = NormalizeOptional(value);
        return normalized ?? throw new InvalidOperationException(message);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static GitAccountResult ToResult(GitAccountEntity entity)
    {
        return new GitAccountResult(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Username,
            entity.AccessToken,
            entity.Description,
            entity.Status,
            entity.CreatedBy,
            entity.CreateTime);
    }

    private static GitRepositoryResult ToResult(GitRepositoryEntity entity)
    {
        return new GitRepositoryResult(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.RepositoryUrl,
            entity.DefaultBranch,
            entity.GitAccountId,
            entity.LocalPath,
            entity.Description,
            entity.Status,
            entity.CreatedBy,
            entity.CreateTime);
    }

    private static GitBranchOperationResult ToResult(GitBranchOperationEntity entity)
    {
        return new GitBranchOperationResult(
            entity.Id,
            entity.RepositoryId,
            entity.AccountId,
            entity.OperationType,
            entity.BranchName,
            entity.SourceBranch,
            entity.BackupBranch,
            entity.CommitHash,
            entity.CommitMessage,
            entity.PushedAt,
            entity.Status,
            entity.Message,
            entity.CreatedBy,
            entity.CreateTime);
    }
}
