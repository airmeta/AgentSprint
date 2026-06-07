using AgentSprint.Model.Modules.Security.Dtos;

namespace AgentSprint.Service.Services.SecurityServices;

public interface ISystemManagementService
{
    Task<IReadOnlyList<UserManagementResult>> ListUsersAsync();

    Task<UserManagementResult> UpsertUserAsync(UpsertUserRequest request);

    Task<bool> DeleteUserAsync(string id);

    Task<IReadOnlyList<RoleManagementResult>> ListRolesAsync();

    Task<RoleManagementResult> UpsertRoleAsync(UpsertRoleRequest request);

    Task<bool> DeleteRoleAsync(string id);

    Task<IReadOnlyList<MenuManagementResult>> ListMenusAsync();

    Task<MenuManagementResult> UpsertMenuAsync(UpsertMenuRequest request);

    /// <summary>
    /// zh-cn: 删除菜单主数据，并由实现决定是否清理依附该菜单的按钮权限等从属授权资源。
    /// en-us: Deletes menu master data and lets the implementation clean dependent authorization resources such as button permissions attached to that menu.
    /// </summary>
    Task<bool> DeleteMenuAsync(string id);

    Task<IReadOnlyList<PermissionManagementResult>> ListPermissionsAsync();

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

    Task<IReadOnlyList<DepartmentManagementResult>> ListDepartmentsAsync();

    Task<DepartmentManagementResult> UpsertDepartmentAsync(UpsertDepartmentRequest request);

    /// <summary>
    /// zh-cn: 软删除指定部门，部门下级和用户部门关联不会被自动物理移除。
    /// en-us: Soft-deletes the specified department without automatically physically removing child departments or user-department associations.
    /// </summary>
    Task<bool> DeleteDepartmentAsync(string id);

    Task<IReadOnlyList<AssignmentManagementResult>> ListAssignmentsAsync();

    Task<AssignmentManagementResult> UpsertAssignmentAsync(UpsertAssignmentRequest request);

    /// <summary>
    /// zh-cn: 软删除指定岗位，既有用户岗位关联保留在通用关联表中供后续维护。
    /// en-us: Soft-deletes the specified assignment while keeping existing user-assignment links in the generic association table for later maintenance.
    /// </summary>
    Task<bool> DeleteAssignmentAsync(string id);

    Task<IReadOnlyList<SecurityAssociationResult>> ListAssociationsAsync();

    Task<SecurityAssociationResult> CreateAssociationAsync(SecurityAssociationRequest request);

    Task<bool> DeleteAssociationAsync(string id);
}
