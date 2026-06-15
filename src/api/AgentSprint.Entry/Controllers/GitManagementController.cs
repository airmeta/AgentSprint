using System.Security.Claims;

using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Authorize]
[Route("git")]
public sealed class GitManagementController : ControllerBase
{
    private readonly IGitManagementService _gitManagementService;

    /// <summary>
    /// zh-cn: 创建 Git 管理控制器，暴露账户、仓库、分支新增、备份删除和推送记录读取接口。
    /// en-us: Creates the Git management controller exposing account, repository, branch creation, backup-before-delete, and push-record endpoints.
    /// </summary>
    public GitManagementController(IGitManagementService gitManagementService)
    {
        _gitManagementService = gitManagementService;
    }

    /// <summary>
    /// zh-cn: 新增 Git 账户。
    /// en-us: Creates a Git account.
    /// </summary>
    [HttpPost("accounts")]
    public Task<ActionResult<ApiResponse<GitAccountResult>>> CreateAccount(SaveGitAccountRequest request)
    {
        return Execute(() => _gitManagementService.CreateAccountAsync(request, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 查询 Git 账户列表。
    /// en-us: Lists Git accounts.
    /// </summary>
    [HttpGet("accounts")]
    public async Task<ApiResponse<IReadOnlyList<GitAccountResult>>> ListAccounts(
        [FromQuery] string? keyword,
        [FromQuery] string? status)
    {
        return ApiResponse<IReadOnlyList<GitAccountResult>>.Ok(
            await _gitManagementService.ListAccountsAsync(keyword, status));
    }

    /// <summary>
    /// zh-cn: 更新 Git 账户。
    /// en-us: Updates a Git account.
    /// </summary>
    [HttpPut("accounts/{id}")]
    public Task<ActionResult<ApiResponse<GitAccountResult>>> UpdateAccount(
        string id,
        SaveGitAccountRequest request)
    {
        return Execute(() => _gitManagementService.UpdateAccountAsync(id, request));
    }

    /// <summary>
    /// zh-cn: 新增 Git 仓库。
    /// en-us: Creates a Git repository.
    /// </summary>
    [HttpPost("repositories")]
    public Task<ActionResult<ApiResponse<GitRepositoryResult>>> CreateRepository(SaveGitRepositoryRequest request)
    {
        return Execute(() => _gitManagementService.CreateRepositoryAsync(request, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 查询 Git 仓库列表。
    /// en-us: Lists Git repositories.
    /// </summary>
    [HttpGet("repositories")]
    public async Task<ApiResponse<IReadOnlyList<GitRepositoryResult>>> ListRepositories(
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] string? gitAccountId)
    {
        return ApiResponse<IReadOnlyList<GitRepositoryResult>>.Ok(
            await _gitManagementService.ListRepositoriesAsync(keyword, status, gitAccountId));
    }

    /// <summary>
    /// zh-cn: 更新 Git 仓库。
    /// en-us: Updates a Git repository.
    /// </summary>
    [HttpPut("repositories/{id}")]
    public Task<ActionResult<ApiResponse<GitRepositoryResult>>> UpdateRepository(
        string id,
        SaveGitRepositoryRequest request)
    {
        return Execute(() => _gitManagementService.UpdateRepositoryAsync(id, request));
    }

    /// <summary>
    /// zh-cn: 新增仓库分支并推送远端。
    /// en-us: Creates a repository branch and pushes it to the remote.
    /// </summary>
    [HttpPost("repositories/{id}/branches")]
    public Task<ActionResult<ApiResponse<GitBranchOperationResult>>> CreateBranch(
        string id,
        CreateGitBranchRequest request)
    {
        return Execute(() => _gitManagementService.CreateBranchAsync(id, request, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 备份后删除仓库分支。
    /// en-us: Deletes a repository branch after creating a backup branch.
    /// </summary>
    [HttpPost("repositories/{id}/branches/delete")]
    public Task<ActionResult<ApiResponse<GitBranchOperationResult>>> DeleteBranch(
        string id,
        DeleteGitBranchRequest request)
    {
        return Execute(() => _gitManagementService.DeleteBranchAsync(id, request, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 读取仓库分支推送记录。
    /// en-us: Reads repository branch push records.
    /// </summary>
    [HttpGet("repositories/{id}/push-records")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GitBranchOperationResult>>>> ReadPushRecords(
        string id,
        [FromQuery] string? branch)
    {
        try
        {
            return ApiResponse<IReadOnlyList<GitBranchOperationResult>>.Ok(
                await _gitManagementService.ReadBranchPushRecordsAsync(id, branch, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<IReadOnlyList<GitBranchOperationResult>>.Error(ex.Message, 400));
        }
    }

    private async Task<ActionResult<ApiResponse<T>>> Execute<T>(Func<Task<T>> action)
    {
        try
        {
            return ApiResponse<T>.Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<T>.Error(ex.Message, 400));
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    }
}
