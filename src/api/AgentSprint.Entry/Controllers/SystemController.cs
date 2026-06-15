using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services.SecurityServices;

using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Authorize(Roles = "super")]
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
    public async Task<ApiResponse<IReadOnlyList<UserManagementResult>>> ListUsers(
        [FromQuery] string? keyword,
        [FromQuery] string? roleId,
        [FromQuery] int? status)
    {
        return ApiResponse<IReadOnlyList<UserManagementResult>>.Ok(
            await _systemService.ListUsersAsync(keyword, roleId, status));
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
    public async Task<ApiResponse<IReadOnlyList<RoleManagementResult>>> ListRoles(
        [FromQuery] string? keyword,
        [FromQuery] int? status,
        [FromQuery] int? grantState)
    {
        return ApiResponse<IReadOnlyList<RoleManagementResult>>.Ok(
            await _systemService.ListRolesAsync(keyword, status, grantState));
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
    public async Task<ApiResponse<IReadOnlyList<MenuManagementResult>>> ListMenus(
        [FromQuery] string? keyword,
        [FromQuery] int? type,
        [FromQuery] int? status)
    {
        return ApiResponse<IReadOnlyList<MenuManagementResult>>.Ok(
            await _systemService.ListMenusAsync(keyword, type, status));
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
    public async Task<ApiResponse<IReadOnlyList<PermissionManagementResult>>> ListPermissions(
        [FromQuery] string? keyword,
        [FromQuery] string? menuId)
    {
        return ApiResponse<IReadOnlyList<PermissionManagementResult>>.Ok(
            await _systemService.ListPermissionsAsync(keyword, menuId));
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
    public async Task<ApiResponse<IReadOnlyList<AgentTokenManagementResult>>> ListAgentTokens(
        [FromQuery] string? keyword,
        [FromQuery] int? status)
    {
        return ApiResponse<IReadOnlyList<AgentTokenManagementResult>>.Ok(
            await _agentTokenService.ListTokensAsync(GetUserId(), GetRoles(), keyword, status));
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
    public async Task<ApiResponse<IReadOnlyList<SystemConfigurationResult>>> ListConfigurations(
        [FromQuery] string? keyword,
        [FromQuery] int? status)
    {
        return ApiResponse<IReadOnlyList<SystemConfigurationResult>>.Ok(
            await _configurationService.ListConfigurationsAsync(keyword, status));
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

    [HttpGet("ai-platforms")]
    public async Task<ApiResponse<IReadOnlyList<AiPlatformResult>>> ListAiPlatforms(
        [FromQuery] string? keyword,
        [FromQuery] int? status)
    {
        return ApiResponse<IReadOnlyList<AiPlatformResult>>.Ok(
            await _configurationService.ListAiPlatformsAsync(keyword, status));
    }

    [HttpPost("ai-platforms")]
    public async Task<ActionResult<ApiResponse<AiPlatformResult>>> UpsertAiPlatform(
        UpsertAiPlatformRequest request)
    {
        return await ExecuteAsync(() => _configurationService.UpsertAiPlatformAsync(request));
    }

    [HttpDelete("ai-platforms/{id}")]
    public async Task<ApiResponse<bool>> DeleteAiPlatform(string id)
    {
        return ApiResponse<bool>.Ok(await _configurationService.DeleteAiPlatformAsync(id));
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
    public async Task<ApiResponse<IReadOnlyList<DepartmentManagementResult>>> ListDepartments(
        [FromQuery] string? keyword,
        [FromQuery] int? status)
    {
        return ApiResponse<IReadOnlyList<DepartmentManagementResult>>.Ok(
            await _systemService.ListDepartmentsAsync(keyword, status));
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
    public async Task<ApiResponse<IReadOnlyList<AssignmentManagementResult>>> ListAssignments(
        [FromQuery] string? keyword,
        [FromQuery] int? status)
    {
        return ApiResponse<IReadOnlyList<AssignmentManagementResult>>.Ok(
            await _systemService.ListAssignmentsAsync(keyword, status));
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

    /// <summary>
    /// <para>zh-cn:查询系统字典类型列表，用于维护业务枚举分类以及驱动字典项过滤。</para>
    /// <para>en-us:Lists system dictionary types used to maintain business enum categories and drive dictionary-item filtering.</para>
    /// </summary>
    [HttpGet("dictionary-types")]
    public async Task<ApiResponse<IReadOnlyList<DictionaryTypeManagementResult>>> ListDictionaryTypes(
        [FromQuery] string? keyword,
        [FromQuery] int? status)
    {
        return ApiResponse<IReadOnlyList<DictionaryTypeManagementResult>>.Ok(
            await _systemService.ListDictionaryTypesAsync(keyword, status));
    }

    /// <summary>
    /// <para>zh-cn:新增或更新字典类型；编码唯一，排序和状态用于控制管理端展示与业务可用性。</para>
    /// <para>en-us:Creates or updates a dictionary type; the unique code, sort value, and status control management display and business availability.</para>
    /// </summary>
    [HttpPost("dictionary-types")]
    public async Task<ActionResult<ApiResponse<DictionaryTypeManagementResult>>> UpsertDictionaryType(
        UpsertDictionaryTypeRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertDictionaryTypeAsync(request));
    }

    /// <summary>
    /// <para>zh-cn:软删除字典类型，同时由服务层清理其下字典项，避免留下无父级的字典值。</para>
    /// <para>en-us:Soft-deletes a dictionary type while the service layer cleans its items to avoid dictionary values without a parent type.</para>
    /// </summary>
    [HttpDelete("dictionary-types/{id}")]
    public async Task<ApiResponse<bool>> DeleteDictionaryType(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteDictionaryTypeAsync(id));
    }

    /// <summary>
    /// <para>zh-cn:查询字典项列表；可通过 dictionaryTypeId 查询参数限定到某个字典类型。</para>
    /// <para>en-us:Lists dictionary items; the dictionaryTypeId query parameter can restrict results to one dictionary type.</para>
    /// </summary>
    [HttpGet("dictionary-items")]
    public async Task<ApiResponse<IReadOnlyList<DictionaryItemManagementResult>>> ListDictionaryItems(
        [FromQuery] string? dictionaryTypeId,
        [FromQuery] string? keyword,
        [FromQuery] int? status)
    {
        return ApiResponse<IReadOnlyList<DictionaryItemManagementResult>>.Ok(
            await _systemService.ListDictionaryItemsAsync(dictionaryTypeId, keyword, status));
    }

    /// <summary>
    /// <para>zh-cn:新增或更新字典项；字典项必须归属到有效字典类型，且同一类型下编码唯一。</para>
    /// <para>en-us:Creates or updates a dictionary item; the item must belong to a valid dictionary type and have a unique code within that type.</para>
    /// </summary>
    [HttpPost("dictionary-items")]
    public async Task<ActionResult<ApiResponse<DictionaryItemManagementResult>>> UpsertDictionaryItem(
        UpsertDictionaryItemRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertDictionaryItemAsync(request));
    }

    /// <summary>
    /// <para>zh-cn:软删除指定字典项，不影响字典类型和其他条目。</para>
    /// <para>en-us:Soft-deletes the specified dictionary item without affecting the dictionary type or sibling entries.</para>
    /// </summary>
    [HttpDelete("dictionary-items/{id}")]
    public async Task<ApiResponse<bool>> DeleteDictionaryItem(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteDictionaryItemAsync(id));
    }

    /// <summary>
    /// <para>zh-cn:查询运行环境列表，可按项目、端和模块过滤，供系统管理维护测试环境与部署信息。</para>
    /// <para>en-us:Lists runtime environments, optionally filtered by project, endpoint, and module for maintaining test-environment and deployment information.</para>
    /// </summary>
    [HttpGet("runtime-environments")]
    public async Task<ApiResponse<IReadOnlyList<RuntimeEnvironmentManagementResult>>> ListRuntimeEnvironments(
        [FromQuery] string? projectId,
        [FromQuery] string? endpointId,
        [FromQuery] string? moduleId)
    {
        return ApiResponse<IReadOnlyList<RuntimeEnvironmentManagementResult>>.Ok(
            await _systemService.ListRuntimeEnvironmentsAsync(projectId, endpointId, moduleId));
    }

    /// <summary>
    /// <para>zh-cn:新增或更新运行环境主数据，包含地址、部署路径、Compose 文件和本地发布包路径等拆分字段。</para>
    /// <para>en-us:Creates or updates runtime environment master data including separated URL, deployment path, compose file, and local package fields.</para>
    /// </summary>
    [HttpPost("runtime-environments")]
    public async Task<ActionResult<ApiResponse<RuntimeEnvironmentManagementResult>>> UpsertRuntimeEnvironment(
        UpsertRuntimeEnvironmentRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertRuntimeEnvironmentAsync(request));
    }

    /// <summary>
    /// <para>zh-cn:软删除运行环境，并由服务层同步清理该环境下服务配置。</para>
    /// <para>en-us:Soft-deletes a runtime environment while the service layer cleans service configurations under it.</para>
    /// </summary>
    [HttpDelete("runtime-environments/{id}")]
    public async Task<ApiResponse<bool>> DeleteRuntimeEnvironment(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteRuntimeEnvironmentAsync(id));
    }

    /// <summary>
    /// <para>zh-cn:查询指定运行环境下的服务配置。</para>
    /// <para>en-us:Lists service configurations under the specified runtime environment.</para>
    /// </summary>
    [HttpGet("runtime-environment-containers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RuntimeEnvironmentContainerManagementResult>>>> ListRuntimeEnvironmentContainers(
        [FromQuery] string runtimeEnvironmentId)
    {
        return await ExecuteAsync(() => _systemService.ListRuntimeEnvironmentContainersAsync(runtimeEnvironmentId));
    }

    /// <summary>
    /// <para>zh-cn:新增或更新运行环境服务配置，维护服务名称、运行容器类型、服务 IP、端口和协议。</para>
    /// <para>en-us:Creates or updates a runtime-environment service configuration with service name, container type, service IP, ports, and protocol.</para>
    /// </summary>
    [HttpPost("runtime-environment-containers")]
    public async Task<ActionResult<ApiResponse<RuntimeEnvironmentContainerManagementResult>>> UpsertRuntimeEnvironmentContainer(
        UpsertRuntimeEnvironmentContainerRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertRuntimeEnvironmentContainerAsync(request));
    }

    /// <summary>
    /// <para>zh-cn:软删除指定运行环境服务配置。</para>
    /// <para>en-us:Soft-deletes the specified runtime-environment service configuration.</para>
    /// </summary>
    [HttpDelete("runtime-environment-containers/{id}")]
    public async Task<ApiResponse<bool>> DeleteRuntimeEnvironmentContainer(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeleteRuntimeEnvironmentContainerAsync(id));
    }

    /// <summary>
    /// <para>zh-cn:查询提示词模板；可通过 agentEnvironment 查询参数限定到某个 AI 平台，未传时返回全部平台模板。</para>
    /// <para>en-us:Lists prompt templates; the agentEnvironment query parameter restricts results to one AI platform, and omission returns templates for all platforms.</para>
    /// </summary>
    [HttpGet("prompt-templates")]
    public async Task<ApiResponse<IReadOnlyList<PromptTemplateManagementResult>>> ListPromptTemplates(
        [FromQuery] string? agentEnvironment,
        [FromQuery] string? keyword,
        [FromQuery] int? status)
    {
        return ApiResponse<IReadOnlyList<PromptTemplateManagementResult>>.Ok(
            await _systemService.ListPromptTemplatesAsync(agentEnvironment, keyword, status));
    }

    /// <summary>
    /// <para>zh-cn:新增或更新提示词模板，服务层按 AI 平台和模板编码维护唯一记录。</para>
    /// <para>en-us:Creates or updates a prompt template; the service layer maintains one unique record per AI platform and template code.</para>
    /// </summary>
    [HttpPost("prompt-templates")]
    public async Task<ActionResult<ApiResponse<PromptTemplateManagementResult>>> UpsertPromptTemplate(
        UpsertPromptTemplateRequest request)
    {
        return await ExecuteAsync(() => _systemService.UpsertPromptTemplateAsync(request));
    }

    /// <summary>
    /// <para>zh-cn:软删除提示词模板。</para>
    /// <para>en-us:Soft-deletes a prompt template.</para>
    /// </summary>
    [HttpDelete("prompt-templates/{id}")]
    public async Task<ApiResponse<bool>> DeletePromptTemplate(string id)
    {
        return ApiResponse<bool>.Ok(await _systemService.DeletePromptTemplateAsync(id));
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
