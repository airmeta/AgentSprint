using AgentSprint.Model.Modules.Agile.Dtos;

namespace AgentSprint.Service.Services.AgileServices;

public interface IGitManagementService
{
    /// <summary>
    /// zh-cn: 创建 Git 账户，账户编码全局唯一，令牌原样保存供仓库操作 runner 注入认证信息；调用方负责控制接口权限和令牌可见范围。
    /// en-us: Creates a Git account with a globally unique code; the token is stored as supplied so repository-operation runners can inject authentication, while callers remain responsible for endpoint authorization and token visibility.
    /// </summary>
    Task<GitAccountResult> CreateAccountAsync(SaveGitAccountRequest request, string userId);

    /// <summary>
    /// zh-cn: 更新 Git 账户名称、用户名、令牌、说明和状态，编码保持不变；空令牌会清除当前令牌。
    /// en-us: Updates a Git account's name, username, token, description, and status while preserving the code; an empty token clears the current token.
    /// </summary>
    Task<GitAccountResult> UpdateAccountAsync(string id, SaveGitAccountRequest request);

    /// <summary>
    /// zh-cn: 查询 Git 账户列表，可按关键字和状态过滤，用于管理页和项目配置下拉选择。
    /// en-us: Lists Git accounts with optional keyword and status filters for management pages and project-configuration selectors.
    /// </summary>
    Task<IReadOnlyList<GitAccountResult>> ListAccountsAsync(string? keyword = null, string? status = null);

    /// <summary>
    /// zh-cn: 创建 Git 仓库记录，仓库 URL 必须是 http 或 https 地址，并可绑定默认 Git 账户和本地工作副本路径。
    /// en-us: Creates a Git repository record; the URL must be http or https and may bind a default Git account and local working-copy path.
    /// </summary>
    Task<GitRepositoryResult> CreateRepositoryAsync(SaveGitRepositoryRequest request, string userId);

    /// <summary>
    /// zh-cn: 更新 Git 仓库记录，编码保持不变；仓库地址、默认分支、绑定账户和本地路径会影响后续分支操作。
    /// en-us: Updates a Git repository while preserving the code; URL, default branch, linked account, and local path affect later branch operations.
    /// </summary>
    Task<GitRepositoryResult> UpdateRepositoryAsync(string id, SaveGitRepositoryRequest request);

    /// <summary>
    /// zh-cn: 查询 Git 仓库列表，可按关键字、状态和账户过滤，用于仓库管理和项目配置下拉选择。
    /// en-us: Lists Git repositories with optional keyword, status, and account filters for repository management and project-configuration selectors.
    /// </summary>
    Task<IReadOnlyList<GitRepositoryResult>> ListRepositoriesAsync(
        string? keyword = null,
        string? status = null,
        string? gitAccountId = null);

    /// <summary>
    /// zh-cn: 在指定仓库创建新分支，并记录操作结果；执行前会准备本地工作副本并从源分支拉取最新状态。
    /// en-us: Creates a branch in the selected repository and records the operation result; before execution it prepares a local working copy and pulls the latest source branch state.
    /// </summary>
    Task<GitBranchOperationResult> CreateBranchAsync(
        string repositoryId,
        CreateGitBranchRequest request,
        string userId);

    /// <summary>
    /// zh-cn: 删除指定分支前先创建并推送备份分支，随后删除远端和本地目标分支，并记录备份分支名称与执行结果。
    /// en-us: Creates and pushes a backup branch before deleting the target branch, then deletes the remote and local target branch and records the backup name and operation result.
    /// </summary>
    Task<GitBranchOperationResult> DeleteBranchAsync(
        string repositoryId,
        DeleteGitBranchRequest request,
        string userId);

    /// <summary>
    /// zh-cn: 读取仓库分支推送记录，返回最近提交摘要并写入读取快照；branch 为空时读取全部远端分支。
    /// en-us: Reads repository branch push records, returns recent commit summaries, and stores a read snapshot; when branch is empty, all remote branches are read.
    /// </summary>
    Task<IReadOnlyList<GitBranchOperationResult>> ReadBranchPushRecordsAsync(
        string repositoryId,
        string? branch,
        string userId);
}
