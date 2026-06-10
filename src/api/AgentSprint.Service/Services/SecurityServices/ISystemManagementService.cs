using AgentSprint.Model.Modules.Security.Dtos;

namespace AgentSprint.Service.Services.SecurityServices;

public interface ISystemManagementService
{
    Task<IReadOnlyList<UserManagementResult>> ListUsersAsync(
        string? keyword = null,
        string? roleId = null,
        int? status = null);

    Task<UserManagementResult> UpsertUserAsync(UpsertUserRequest request);

    Task<bool> DeleteUserAsync(string id);

    Task<IReadOnlyList<RoleManagementResult>> ListRolesAsync(
        string? keyword = null,
        int? status = null,
        int? grantState = null);

    Task<RoleManagementResult> UpsertRoleAsync(UpsertRoleRequest request);

    Task<bool> DeleteRoleAsync(string id);

    Task<IReadOnlyList<MenuManagementResult>> ListMenusAsync(
        string? keyword = null,
        int? type = null,
        int? status = null);

    Task<MenuManagementResult> UpsertMenuAsync(UpsertMenuRequest request);

    /// <summary>
    /// zh-cn: 删除菜单主数据，并由实现决定是否清理依附该菜单的按钮权限等从属授权资源。
    /// en-us: Deletes menu master data and lets the implementation clean dependent authorization resources such as button permissions attached to that menu.
    /// </summary>
    Task<bool> DeleteMenuAsync(string id);

    Task<IReadOnlyList<PermissionManagementResult>> ListPermissionsAsync(
        string? keyword = null,
        string? menuId = null);

    /// <summary>
    /// zh-cn: 新增或更新菜单下的按钮权限；请求必须携带有效 MenuId，使权限码始终归属到一个菜单。
    /// en-us: Creates or updates a button permission under a menu; requests must include a valid MenuId so every permission code belongs to a menu.
    /// </summary>
    Task<PermissionManagementResult> UpsertPermissionAsync(UpsertPermissionRequest request);

    Task<bool> DeletePermissionAsync(string id);

    Task<IReadOnlyList<UserGroupManagementResult>> ListUserGroupsAsync();

    Task<UserGroupManagementResult> UpsertUserGroupAsync(UpsertUserGroupRequest request);

    /// <summary>
    /// zh-cn: 软删除指定用户组，保留既有通用关联历史，由维护人员按需单独清理关联。
    /// en-us: Soft-deletes the specified user group while preserving existing generic-association history for separate maintenance cleanup.
    /// </summary>
    Task<bool> DeleteUserGroupAsync(string id);

    Task<IReadOnlyList<RoleGroupManagementResult>> ListRoleGroupsAsync();

    Task<RoleGroupManagementResult> UpsertRoleGroupAsync(UpsertRoleGroupRequest request);

    /// <summary>
    /// zh-cn: 软删除指定角色组，不级联删除角色或用户授权关系，避免影响历史授权审计。
    /// en-us: Soft-deletes the specified role group without cascading role or user grants so authorization history remains auditable.
    /// </summary>
    Task<bool> DeleteRoleGroupAsync(string id);

    Task<IReadOnlyList<DepartmentManagementResult>> ListDepartmentsAsync(string? keyword = null, int? status = null);

    Task<DepartmentManagementResult> UpsertDepartmentAsync(UpsertDepartmentRequest request);

    /// <summary>
    /// zh-cn: 软删除指定部门，部门下级和用户部门关联不会被自动物理移除。
    /// en-us: Soft-deletes the specified department without automatically physically removing child departments or user-department associations.
    /// </summary>
    Task<bool> DeleteDepartmentAsync(string id);

    Task<IReadOnlyList<AssignmentManagementResult>> ListAssignmentsAsync(string? keyword = null, int? status = null);

    Task<AssignmentManagementResult> UpsertAssignmentAsync(UpsertAssignmentRequest request);

    /// <summary>
    /// zh-cn: 软删除指定岗位，既有用户岗位关联保留在通用关联表中供后续维护。
    /// en-us: Soft-deletes the specified assignment while keeping existing user-assignment links in the generic association table for later maintenance.
    /// </summary>
    Task<bool> DeleteAssignmentAsync(string id);

    /// <summary>
    /// <para>zh-cn:查询未删除的字典类型，按排序和编码稳定返回，供系统管理页面维护可复用枚举分类。</para>
    /// <para>en-us:Lists non-deleted dictionary types in a stable sort/code order for maintaining reusable enum categories in system management.</para>
    /// </summary>
    Task<IReadOnlyList<DictionaryTypeManagementResult>> ListDictionaryTypesAsync(
        string? keyword = null,
        int? status = null);

    /// <summary>
    /// <para>zh-cn:新增或更新字典类型；编码在字典类型范围内唯一，软删除记录可通过相同 Id 恢复。</para>
    /// <para>en-us:Creates or updates a dictionary type; the type code is unique across dictionary types and soft-deleted rows can be restored by the same id.</para>
    /// </summary>
    Task<DictionaryTypeManagementResult> UpsertDictionaryTypeAsync(UpsertDictionaryTypeRequest request);

    /// <summary>
    /// <para>zh-cn:软删除字典类型，并同步软删除该类型下的字典项，避免保留无法维护的孤立条目。</para>
    /// <para>en-us:Soft-deletes a dictionary type and its items together so orphaned entries are not left without a maintainable parent type.</para>
    /// </summary>
    Task<bool> DeleteDictionaryTypeAsync(string id);

    /// <summary>
    /// <para>zh-cn:查询字典项；传入字典类型编号时仅返回该类型下的条目，否则返回全部未删除条目。</para>
    /// <para>en-us:Lists dictionary items; when a dictionary type id is supplied, only entries under that type are returned, otherwise all non-deleted entries are returned.</para>
    /// </summary>
    Task<IReadOnlyList<DictionaryItemManagementResult>> ListDictionaryItemsAsync(
        string? dictionaryTypeId = null,
        string? keyword = null,
        int? status = null);

    /// <summary>
    /// <para>zh-cn:新增或更新字典项；必须归属到存在的字典类型，且同一字典类型内编码唯一。</para>
    /// <para>en-us:Creates or updates a dictionary item; it must belong to an existing dictionary type, and codes are unique within that type.</para>
    /// </summary>
    Task<DictionaryItemManagementResult> UpsertDictionaryItemAsync(UpsertDictionaryItemRequest request);

    /// <summary>
    /// <para>zh-cn:软删除指定字典项，不影响同类型下其他条目。</para>
    /// <para>en-us:Soft-deletes the specified dictionary item without affecting sibling entries under the same type.</para>
    /// </summary>
    Task<bool> DeleteDictionaryItemAsync(string id);

    /// <summary>
    /// <para>zh-cn:查询运行环境主数据，可按项目、端和模块过滤，用于维护测试环境地址、部署路径和发布包位置。</para>
    /// <para>en-us:Lists runtime environment master data, optionally filtered by project, endpoint, and module for maintaining test URLs, deployment paths, and package locations.</para>
    /// </summary>
    Task<IReadOnlyList<RuntimeEnvironmentManagementResult>> ListRuntimeEnvironmentsAsync(
        string? projectId = null,
        string? endpointId = null,
        string? moduleId = null);

    /// <summary>
    /// <para>zh-cn:新增或更新运行环境；编码在同一项目范围内唯一，归属字段保留给多端和微服务部署适配。</para>
    /// <para>en-us:Creates or updates a runtime environment; codes are unique within the project scope, and ownership fields support later endpoint and microservice deployment adaptation.</para>
    /// </summary>
    Task<RuntimeEnvironmentManagementResult> UpsertRuntimeEnvironmentAsync(UpsertRuntimeEnvironmentRequest request);

    /// <summary>
    /// <para>zh-cn:软删除运行环境，并同步软删除该环境下的容器映射，避免留下无法维护的部署明细。</para>
    /// <para>en-us:Soft-deletes a runtime environment and its service configurations together so deployment details do not remain without a maintainable parent.</para>
    /// </summary>
    Task<bool> DeleteRuntimeEnvironmentAsync(string id);

    /// <summary>
    /// <para>zh-cn:查询指定运行环境下的容器端口映射，按排序和容器名称稳定返回。</para>
    /// <para>en-us:Lists service configurations under the specified runtime environment in a stable sort/name order.</para>
    /// </summary>
    Task<IReadOnlyList<RuntimeEnvironmentContainerManagementResult>> ListRuntimeEnvironmentContainersAsync(
        string runtimeEnvironmentId);

    /// <summary>
    /// <para>zh-cn:新增或更新运行环境容器映射；同一运行环境内容器名称唯一，端口必须为有效 TCP/UDP 端口号。</para>
    /// <para>en-us:Creates or updates a runtime environment service configuration; names are unique within one environment and ports must be valid TCP/UDP port numbers.</para>
    /// </summary>
    Task<RuntimeEnvironmentContainerManagementResult> UpsertRuntimeEnvironmentContainerAsync(
        UpsertRuntimeEnvironmentContainerRequest request);

    /// <summary>
    /// <para>zh-cn:软删除指定容器映射，不影响运行环境主数据和其他容器。</para>
    /// <para>en-us:Soft-deletes the specified service configuration without changing the runtime environment or sibling services.</para>
    /// </summary>
    Task<bool> DeleteRuntimeEnvironmentContainerAsync(string id);

    /// <summary>
    /// <para>zh-cn:查询提示词模板；可按 AI 平台过滤，未传平台时返回全部未删除模板。</para>
    /// <para>en-us:Lists prompt templates; filters by AI platform when supplied and returns all non-deleted templates when omitted.</para>
    /// </summary>
    Task<IReadOnlyList<PromptTemplateManagementResult>> ListPromptTemplatesAsync(
        string? agentEnvironment = null,
        string? keyword = null,
        int? status = null);

    /// <summary>
    /// <para>zh-cn:新增或更新提示词模板；同一 AI 平台下模板编码唯一，内容应使用模板变量承载令牌、地址和任务上下文。</para>
    /// <para>en-us:Creates or updates a prompt template; template code is unique within one AI platform and content should use placeholders for tokens, URLs, and task context.</para>
    /// </summary>
    Task<PromptTemplateManagementResult> UpsertPromptTemplateAsync(UpsertPromptTemplateRequest request);

    /// <summary>
    /// <para>zh-cn:软删除提示词模板，不影响同一环境下其他模板。</para>
    /// <para>en-us:Soft-deletes a prompt template without affecting other templates in the same environment.</para>
    /// </summary>
    Task<bool> DeletePromptTemplateAsync(string id);

    Task<IReadOnlyList<SecurityAssociationResult>> ListAssociationsAsync();

    Task<SecurityAssociationResult> CreateAssociationAsync(SecurityAssociationRequest request);

    Task<bool> DeleteAssociationAsync(string id);
}
