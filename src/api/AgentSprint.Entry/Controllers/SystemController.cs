using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services.SecurityServices;

using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Authorize]
[Route("system")]
public sealed class SystemController : ControllerBase
{
    private readonly ISystemManagementService _systemService;
    private readonly IAgentTokenService _agentTokenService;
    private readonly ISystemConfigurationService _configurationService;

    /// <summary>
    /// zh-cn: 创建系统管理控制器，暴露当前项目范围内的 RBAC 主体、菜单、权限码和通用关联表维护接口。
    /// en-us: Creates the system management controller exposing maintenance APIs for RBAC subjects, menus, permission codes, and generic associations in the current project scope.
    /// </summary>
    /// <param name="systemService">
    /// zh-cn: 系统管理服务，负责校验、写入和同步通用关联与旧 RBAC 关系。
    /// en-us: System management service responsible for validation, persistence, and synchronization between generic associations and legacy RBAC relations.
    /// </param>
    public SystemController(
        ISystemManagementService systemService,
        IAgentTokenService agentTokenService,
        ISystemConfigurationService configurationService)
    {
        _systemService = systemService;
        _agentTokenService = agentTokenService;
        _configurationService = configurationService;
    }

    [HttpGet("users")]
    public async Task<ApiResponse<IReadOnlyList<UserManagementResult>>> ListUsers()
    {
        return ApiResponse<IReadOnlyList<UserManagementResult>>.Ok(await _systemService.ListUsersAsync());
    }

    [HttpPost("users")]
    public async Task<ActionResult<ApiResponse<UserManagementResult>>> UpsertUser(UpsertUserRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertUserAsync(request));
    }

    [HttpDelete("users/{id}")]
    public async Task<ApiResponse<bool>> DeleteUser(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteUserAsync(id));
    }

    [HttpGet("roles")]
    public async Task<ApiResponse<IReadOnlyList<RoleManagementResult>>> ListRoles()
    {
        return ApiResponse<IReadOnlyList<RoleManagementResult>>.Ok(await _systemService.ListRolesAsync());
    }

    [HttpPost("roles")]
    public async Task<ActionResult<ApiResponse<RoleManagementResult>>> UpsertRole(UpsertRoleRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertRoleAsync(request));
    }

    [HttpDelete("roles/{id}")]
    public async Task<ApiResponse<bool>> DeleteRole(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteRoleAsync(id));
    }

    [HttpGet("menus")]
    public async Task<ApiResponse<IReadOnlyList<MenuManagementResult>>> ListMenus()
    {
        return ApiResponse<IReadOnlyList<MenuManagementResult>>.Ok(await _systemService.ListMenusAsync());
    }

    [HttpPost("menus")]
    public async Task<ActionResult<ApiResponse<MenuManagementResult>>> UpsertMenu(UpsertMenuRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertMenuAsync(request));
    }

    [HttpDelete("menus/{id}")]
    public async Task<ApiResponse<bool>> DeleteMenu(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteMenuAsync(id));
    }

    [HttpGet("permissions")]
    public async Task<ApiResponse<IReadOnlyList<PermissionManagementResult>>> ListPermissions()
    {
        return ApiResponse<IReadOnlyList<PermissionManagementResult>>.Ok(await _systemService.ListPermissionsAsync());
    }

    [HttpPost("permissions")]
    public async Task<ActionResult<ApiResponse<PermissionManagementResult>>> UpsertPermission(UpsertPermissionRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertPermissionAsync(request));
    }

    [HttpDelete("permissions/{id}")]
    public async Task<ApiResponse<bool>> DeletePermission(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeletePermissionAsync(id));
    }

    /// <summary>
    /// zh-cn: 查询当前用户可见的 Agent 令牌。普通用户只能看到自己的令牌，超级管理员可以看到全部令牌。
    /// en-us: Lists Agent tokens visible to the current user. Normal users can see only their own tokens, while super administrators can see all tokens.
    /// </summary>
    /// <returns>
    /// zh-cn: 不包含完整令牌明文的令牌元数据集合。
    /// en-us: Token metadata collection without full token plaintext.
    /// </returns>
    [HttpGet("agent-tokens")]
    public async Task<ApiResponse<IReadOnlyList<AgentTokenManagementResult>>> ListAgentTokens()
    {
        return ApiResponse<IReadOnlyList<AgentTokenManagementResult>>.Ok(
            await _agentTokenService.ListTokensAsync(GetUserId(), GetRoles()));
    }

    /// <summary>
    /// zh-cn: 创建 Agent 令牌。完整 64 位随机串仅在本次响应中返回一次，后续列表只展示掩码。
    /// en-us: Creates an Agent token. The full 64-character random string is returned only in this response; later list responses show only a mask.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 令牌名称、到期时间、可选项目和可选归属用户。
    /// en-us: Token name, expiration, optional project, and optional owner.
    /// </param>
    /// <returns>
    /// zh-cn: 新令牌明文和展示元数据。
    /// en-us: Newly generated token plaintext and display metadata.
    /// </returns>
    [HttpPost("agent-tokens")]
    public async Task<ActionResult<ApiResponse<CreatedAgentTokenResult>>> CreateAgentToken(CreateAgentTokenRequest request)
    {
        return await ExecuteAsync(() => _agentTokenService.CreateTokenAsync(request, GetUserId(), GetRoles()));
    }

    /// <summary>
    /// zh-cn: 撤销指定 Agent 令牌。普通用户只能撤销自己的令牌，超级管理员可以撤销任意令牌。
    /// en-us: Revokes the specified Agent token. Normal users can revoke only their own tokens, while super administrators can revoke any token.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 令牌记录编号。
    /// en-us: Token record id.
    /// </param>
    /// <returns>
    /// zh-cn: 撤销结果。
    /// en-us: Revocation result.
    /// </returns>
    [HttpDelete("agent-tokens/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> RevokeAgentToken(string id)
    {
        return await ExecuteAsync(() => _agentTokenService.RevokeTokenAsync(id, GetUserId(), GetRoles()));
    }

    /// <summary>
    /// zh-cn: 查询系统配置项，用于维护 MCP 地址等运行时可调整参数。
    /// en-us: Lists system settings used to maintain runtime-adjustable parameters such as the MCP endpoint.
    /// </summary>
    /// <returns>
    /// zh-cn: 配置项列表。
    /// en-us: Configuration list.
    /// </returns>
    [HttpGet("configurations")]
    public async Task<ApiResponse<IReadOnlyList<SystemConfigurationResult>>> ListConfigurations()
    {
        return ApiResponse<IReadOnlyList<SystemConfigurationResult>>.Ok(
            await _configurationService.ListConfigurationsAsync());
    }

    /// <summary>
    /// zh-cn: 新增或更新系统配置项，Key 唯一，Value 不能为空。
    /// en-us: Creates or updates a system setting; Key is unique and Value is required.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 配置保存请求。
    /// en-us: Configuration save request.
    /// </param>
    /// <returns>
    /// zh-cn: 保存后的配置项。
    /// en-us: Saved configuration.
    /// </returns>
    [HttpPost("configurations")]
    public async Task<ActionResult<ApiResponse<SystemConfigurationResult>>> UpsertConfiguration(
        UpsertSystemConfigurationRequest request)
    {
        return await ExecuteAsync(() => _configurationService.UpsertConfigurationAsync(request));
    }

    /// <summary>
    /// zh-cn: 删除指定系统配置项；删除后业务读取会回退默认值。
    /// en-us: Deletes the specified system setting; business reads fall back to defaults after deletion.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 配置项编号。
    /// en-us: Configuration id.
    /// </param>
    /// <returns>
    /// zh-cn: 删除结果。
    /// en-us: Deletion result.
    /// </returns>
    [HttpDelete("configurations/{id}")]
    public async Task<ApiResponse<bool>> DeleteConfiguration(string id)
    {
        return ApiResponse<bool>.Ok(await _configurationService.DeleteConfigurationAsync(id));
    }

    [HttpGet("user-groups")]
    public async Task<ApiResponse<IReadOnlyList<UserGroupManagementResult>>> ListUserGroups()
    {
        return ApiResponse<IReadOnlyList<UserGroupManagementResult>>.Ok(await _systemService.ListUserGroupsAsync());
    }

    [HttpPost("user-groups")]
    public async Task<ActionResult<ApiResponse<UserGroupManagementResult>>> UpsertUserGroup(UpsertUserGroupRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertUserGroupAsync(request));
    }

    /// <summary>
    /// zh-cn: 删除用户组主数据，采用软删除并返回操作结果；授权关联由关联维护接口独立处理。
    /// en-us: Deletes user-group master data using soft deletion and returns the operation result; authorization associations are handled independently through association maintenance APIs.
    /// </summary>
    [HttpDelete("user-groups/{id}")]
    public async Task<ApiResponse<bool>> DeleteUserGroup(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteUserGroupAsync(id));
    }

    [HttpGet("role-groups")]
    public async Task<ApiResponse<IReadOnlyList<RoleGroupManagementResult>>> ListRoleGroups()
    {
        return ApiResponse<IReadOnlyList<RoleGroupManagementResult>>.Ok(await _systemService.ListRoleGroupsAsync());
    }

    [HttpPost("role-groups")]
    public async Task<ActionResult<ApiResponse<RoleGroupManagementResult>>> UpsertRoleGroup(UpsertRoleGroupRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertRoleGroupAsync(request));
    }

    /// <summary>
    /// zh-cn: 删除角色组主数据，采用软删除并保留通用关联历史，便于后续审计或恢复。
    /// en-us: Deletes role-group master data using soft deletion while keeping generic association history for later audit or restoration.
    /// </summary>
    [HttpDelete("role-groups/{id}")]
    public async Task<ApiResponse<bool>> DeleteRoleGroup(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteRoleGroupAsync(id));
    }

    [HttpGet("departments")]
    public async Task<ApiResponse<IReadOnlyList<DepartmentManagementResult>>> ListDepartments()
    {
        return ApiResponse<IReadOnlyList<DepartmentManagementResult>>.Ok(await _systemService.ListDepartmentsAsync());
    }

    [HttpPost("departments")]
    public async Task<ActionResult<ApiResponse<DepartmentManagementResult>>> UpsertDepartment(UpsertDepartmentRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertDepartmentAsync(request));
    }

    /// <summary>
    /// zh-cn: 删除部门主数据，采用软删除且不隐式级联处理子部门或用户部门关系。
    /// en-us: Deletes department master data using soft deletion without implicitly cascading to child departments or user-department links.
    /// </summary>
    [HttpDelete("departments/{id}")]
    public async Task<ApiResponse<bool>> DeleteDepartment(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteDepartmentAsync(id));
    }

    [HttpGet("assignments")]
    public async Task<ApiResponse<IReadOnlyList<AssignmentManagementResult>>> ListAssignments()
    {
        return ApiResponse<IReadOnlyList<AssignmentManagementResult>>.Ok(await _systemService.ListAssignmentsAsync());
    }

    [HttpPost("assignments")]
    public async Task<ActionResult<ApiResponse<AssignmentManagementResult>>> UpsertAssignment(UpsertAssignmentRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertAssignmentAsync(request));
    }

    /// <summary>
    /// zh-cn: 删除岗位主数据，采用软删除并保留通用关联表中的历史用户岗位关系。
    /// en-us: Deletes assignment master data using soft deletion while preserving historical user-assignment links in the generic association table.
    /// </summary>
    [HttpDelete("assignments/{id}")]
    public async Task<ApiResponse<bool>> DeleteAssignment(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteAssignmentAsync(id));
    }

    [HttpGet("associations")]
    public async Task<ApiResponse<IReadOnlyList<SecurityAssociationResult>>> ListAssociations()
    {
        return ApiResponse<IReadOnlyList<SecurityAssociationResult>>.Ok(await _systemService.ListAssociationsAsync());
    }

    [HttpPost("associations")]
    public async Task<ActionResult<ApiResponse<SecurityAssociationResult>>> CreateAssociation(SecurityAssociationRequest request)
    {
        return await ExecuteAsync(() => _systemService.CreateAssociationAsync(request));
    }

    [HttpDelete("associations/{id}")]
    public async Task<ApiResponse<bool>> DeleteAssociation(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteAssociationAsync(id));
    }

    private ActionResult<ApiResponse<T>> BadRequestEnvelope<T>(string message)
    {
        return BadRequest(ApiResponse<T>.Error(message, 400));
    }

    private async Task<ActionResult<ApiResponse<T>>> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return ApiResponse<T>.Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestEnvelope<T>(ex.Message);
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    }

    private IReadOnlyList<string> GetRoles()
    {
        return User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();
    }
}
