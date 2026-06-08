using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Security;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.SecurityServices;

namespace AgentSprint.Service.Impls.SecurityServices;

public sealed class SystemManagementService : AgentSprintServiceBase, ISystemManagementService
{
    private static readonly ISet<string> FixedPromptTemplateCodes = new HashSet<string>(
        ["mcp_setup", "task_execution"],
        StringComparer.OrdinalIgnoreCase);

    private readonly IUserDomain _userDomain;
    private readonly IRoleDomain _roleDomain;
    private readonly IMenuDomain _menuDomain;
    private readonly IPermissionDomain _permissionDomain;
    private readonly IUserGroupDomain _userGroupDomain;
    private readonly IRoleGroupDomain _roleGroupDomain;
    private readonly IDepartmentDomain _departmentDomain;
    private readonly IAssignmentDomain _assignmentDomain;
    private readonly IDictionaryTypeDomain _dictionaryTypeDomain;
    private readonly IDictionaryItemDomain _dictionaryItemDomain;
    private readonly IRuntimeEnvironmentDomain _runtimeEnvironmentDomain;
    private readonly IRuntimeEnvironmentContainerDomain _runtimeEnvironmentContainerDomain;
    private readonly IPromptTemplateDomain _promptTemplateDomain;
    private readonly IUserRoleDomain _userRoleDomain;
    private readonly IRoleMenuDomain _roleMenuDomain;
    private readonly IRolePermissionDomain _rolePermissionDomain;
    private readonly IEntityAssociationDomain _associationDomain;

    /// <summary>
    /// zh-cn: 创建系统管理服务，负责维护当前项目范围内的 RBAC 主体、菜单、权限码和通用关联表，并同步旧关系表以兼容既有 MVP 流程。
    /// en-us: Creates the system management service responsible for maintaining RBAC subjects, menus, permission codes, and generic associations in the current project scope while synchronizing legacy relation tables for existing MVP compatibility.
    /// </summary>
    public SystemManagementService(
        IUserDomain userDomain,
        IRoleDomain roleDomain,
        IMenuDomain menuDomain,
        IPermissionDomain permissionDomain,
        IUserGroupDomain userGroupDomain,
        IRoleGroupDomain roleGroupDomain,
        IDepartmentDomain departmentDomain,
        IAssignmentDomain assignmentDomain,
        IDictionaryTypeDomain dictionaryTypeDomain,
        IDictionaryItemDomain dictionaryItemDomain,
        IRuntimeEnvironmentDomain runtimeEnvironmentDomain,
        IRuntimeEnvironmentContainerDomain runtimeEnvironmentContainerDomain,
        IPromptTemplateDomain promptTemplateDomain,
        IUserRoleDomain userRoleDomain,
        IRoleMenuDomain roleMenuDomain,
        IRolePermissionDomain rolePermissionDomain,
        IEntityAssociationDomain associationDomain)
    {
        _userDomain = userDomain;
        _roleDomain = roleDomain;
        _menuDomain = menuDomain;
        _permissionDomain = permissionDomain;
        _userGroupDomain = userGroupDomain;
        _roleGroupDomain = roleGroupDomain;
        _departmentDomain = departmentDomain;
        _assignmentDomain = assignmentDomain;
        _dictionaryTypeDomain = dictionaryTypeDomain;
        _dictionaryItemDomain = dictionaryItemDomain;
        _runtimeEnvironmentDomain = runtimeEnvironmentDomain;
        _runtimeEnvironmentContainerDomain = runtimeEnvironmentContainerDomain;
        _promptTemplateDomain = promptTemplateDomain;
        _userRoleDomain = userRoleDomain;
        _roleMenuDomain = roleMenuDomain;
        _rolePermissionDomain = rolePermissionDomain;
        _associationDomain = associationDomain;
    }

    /// <summary>
    /// zh-cn: 返回用户列表，并从通用关联表解析用户直连角色；旧 UserRole 仅作为没有通用关联时的兜底。
    /// en-us: Lists users and resolves direct user roles from generic associations; legacy UserRole is used only as a fallback when generic associations are absent.
    /// </summary>
    public async Task<IReadOnlyList<UserManagementResult>> ListUsersAsync()
    {
        var users = await _userDomain.ListAsync();
        var results = new List<UserManagementResult>();
        foreach (var entity in users.OrderBy(entity => entity.Username, StringComparer.Ordinal))
        {
            results.Add(new UserManagementResult(
                entity.Id,
                entity.Username,
                entity.DisplayName,
                entity.Email,
                entity.PhoneNumber,
                entity.Avatar,
                entity.Status,
                await GetTargets(entity.Id, SecurityAssociationTypes.UserRole)));
        }

        return results;
    }

    /// <summary>
    /// zh-cn: 新增或更新用户；新增时必须提供密码，更新时密码为空则保持原密码，并同步用户-角色通用关联和旧 UserRole 表。
    /// en-us: Creates or updates a user; new users require a password, updates keep the existing password when Password is empty, and user-role generic associations are synchronized with legacy UserRole rows.
    /// </summary>
    public async Task<UserManagementResult> UpsertUserAsync(UpsertUserRequest request)
    {
        ValidateRequired(request.Username, "Username is required.");
        ValidateRequired(request.DisplayName, "Display name is required.");

        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? null
            : await _userDomain.GetAsync(request.Id);
        entity ??= await _userDomain.FindAnyByUsernameAsync(request.Username.Trim());

        if (entity is null)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new InvalidOperationException("Password is required for new users.");
            }

            entity = new UserEntity();
            entity.PasswordHash = PasswordHasher.Hash(request.Password);
            await _userDomain.CreateAsync(entity);
        }
        else if (!string.Equals(entity.Username, request.Username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var sameUsername = await _userDomain.FindAnyByUsernameAsync(request.Username.Trim());
            if (sameUsername is not null && sameUsername.Id != entity.Id)
            {
                throw new InvalidOperationException("Username already exists.");
            }
        }

        entity.Username = request.Username.Trim();
        entity.DisplayName = request.DisplayName.Trim();
        entity.Email = NormalizeOptional(request.Email);
        entity.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        entity.Avatar = NormalizeOptional(request.Avatar);
        entity.Status = request.Status;
        entity.IsDelete = 0;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            entity.PasswordHash = PasswordHasher.Hash(request.Password);
        }

        await _userDomain.UpdateAsync(entity);
        await ReplaceAssociationsAsync(entity.Id, SecurityAssociationTypes.UserRole, request.RoleIds ?? []);
        await ReplaceUserRolesAsync(entity.Id, request.RoleIds ?? []);
        return (await ListUsersAsync()).Single(item => item.Id == entity.Id);
    }

    public Task<bool> DeleteUserAsync(string id)
    {
        return _userDomain.DeleteAsync(id);
    }

    public async Task<IReadOnlyList<RoleManagementResult>> ListRolesAsync()
    {
        var roles = await _roleDomain.ListAsync();
        var results = new List<RoleManagementResult>();
        foreach (var entity in roles.OrderBy(entity => entity.Code, StringComparer.Ordinal))
        {
            results.Add(new RoleManagementResult(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.Description,
                entity.Status,
                await GetTargets(entity.Id, SecurityAssociationTypes.RoleMenu),
                await GetTargets(entity.Id, SecurityAssociationTypes.RolePermission)));
        }

        return results;
    }

    public async Task<RoleManagementResult> UpsertRoleAsync(UpsertRoleRequest request)
    {
        ValidateRequired(request.Code, "Role code is required.");
        ValidateRequired(request.Name, "Role name is required.");

        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? null
            : await _roleDomain.GetAsync(request.Id);
        entity ??= await _roleDomain.FindAnyByCodeAsync(request.Code.Trim());
        entity ??= new RoleEntity();

        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.Status = request.Status;
        entity.IsDelete = 0;

        if (string.IsNullOrWhiteSpace(entity.Id) || (await _roleDomain.GetAsync(entity.Id)) is null)
        {
            await _roleDomain.CreateAsync(entity);
        }
        else
        {
            await _roleDomain.UpdateAsync(entity);
        }

        await ReplaceAssociationsAsync(entity.Id, SecurityAssociationTypes.RoleMenu, request.MenuIds ?? []);
        await ReplaceAssociationsAsync(entity.Id, SecurityAssociationTypes.RolePermission, request.PermissionIds ?? []);
        await ReplaceRoleMenusAsync(entity.Id, request.MenuIds ?? []);
        await ReplaceRolePermissionsAsync(entity.Id, request.PermissionIds ?? []);
        return (await ListRolesAsync()).Single(item => item.Id == entity.Id);
    }

    public Task<bool> DeleteRoleAsync(string id)
    {
        return _roleDomain.DeleteAsync(id);
    }

    public async Task<IReadOnlyList<MenuManagementResult>> ListMenusAsync()
    {
        return (await _menuDomain.ListAsync())
            .OrderBy(entity => entity.Sort)
            .Select(MapMenu)
            .ToList();
    }

    public async Task<MenuManagementResult> UpsertMenuAsync(UpsertMenuRequest request)
    {
        ValidateRequired(request.Path, "Menu path is required.");
        ValidateRequired(request.Name, "Menu name is required.");
        var entity = string.IsNullOrWhiteSpace(request.Id) ? new MenuEntity() : await _menuDomain.GetAsync(request.Id) ?? new MenuEntity();
        entity.ParentId = NormalizeOptional(request.ParentId);
        entity.Path = request.Path.Trim();
        entity.Name = request.Name.Trim();
        entity.Component = NormalizeOptional(request.Component);
        entity.Icon = NormalizeOptional(request.Icon);
        entity.Sort = request.Sort;
        entity.Type = request.Type;
        entity.Status = request.Status;
        entity.IsDelete = 0;

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            await _menuDomain.CreateAsync(entity);
        }
        else
        {
            await _menuDomain.UpdateAsync(entity);
        }

        return MapMenu(entity);
    }

    /// <summary>
    /// zh-cn: 软删除指定菜单，并同步软删除直接挂载在该菜单下的按钮权限码，避免菜单管理合并按钮权限后留下无归属的按钮授权项。
    /// en-us: Soft-deletes the specified menu and also soft-deletes button permission codes directly attached to it, preventing orphaned button grants after permissions are managed inside menus.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 要删除的菜单编号；为空时由底层领域对象按现有规则处理。
    /// en-us: Menu id to delete; empty values are handled by the underlying domain according to its existing rules.
    /// </param>
    /// <returns>
    /// zh-cn: 返回菜单软删除是否成功；按钮权限清理由同一调用顺序执行，失败会向调用方传播异常。
    /// en-us: Returns whether menu soft deletion succeeded; button permission cleanup runs in the same call sequence and propagates failures to the caller.
    /// </returns>
    public async Task<bool> DeleteMenuAsync(string id)
    {
        var deleted = await _menuDomain.DeleteAsync(id);
        if (!deleted)
        {
            return false;
        }

        var permissions = await _permissionDomain.ListAsync(entity => entity.MenuId == id);
        foreach (var permission in permissions)
        {
            await _permissionDomain.DeleteAsync(permission.Id);
        }

        return true;
    }

    public async Task<IReadOnlyList<PermissionManagementResult>> ListPermissionsAsync()
    {
        return (await _permissionDomain.ListAsync())
            .OrderBy(entity => entity.Code, StringComparer.Ordinal)
            .Select(MapPermission)
            .ToList();
    }

    public async Task<PermissionManagementResult> UpsertPermissionAsync(UpsertPermissionRequest request)
    {
        ValidateRequired(request.Code, "Permission code is required.");
        ValidateRequired(request.Name, "Permission name is required.");
        ValidateRequired(request.MenuId, "Permission menu is required.");
        var menuId = request.MenuId!.Trim();
        if (await _menuDomain.GetAsync(menuId) is null)
        {
            throw new InvalidOperationException("Permission menu does not exist.");
        }

        var existing = (await _permissionDomain.ListIncludingDeletedAsync(entity => entity.Code == request.Code.Trim()))
            .FirstOrDefault(entity => string.IsNullOrWhiteSpace(request.Id) || entity.Id != request.Id);
        if (existing is not null)
        {
            throw new InvalidOperationException("Permission code already exists.");
        }

        var entity = string.IsNullOrWhiteSpace(request.Id) ? new PermissionEntity() : await _permissionDomain.GetAsync(request.Id) ?? new PermissionEntity();
        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.MenuId = menuId;
        entity.IsDelete = 0;
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            await _permissionDomain.CreateAsync(entity);
        }
        else
        {
            await _permissionDomain.UpdateAsync(entity);
        }

        return MapPermission(entity);
    }

    public Task<bool> DeletePermissionAsync(string id)
    {
        return _permissionDomain.DeleteAsync(id);
    }

    public async Task<IReadOnlyList<UserGroupManagementResult>> ListUserGroupsAsync()
    {
        return (await _userGroupDomain.ListAsync()).OrderBy(entity => entity.Code, StringComparer.Ordinal).Select(MapUserGroup).ToList();
    }

    public async Task<UserGroupManagementResult> UpsertUserGroupAsync(UpsertUserGroupRequest request)
    {
        var entity = await UpsertCodeNameEntityAsync(
            _userGroupDomain,
            request.Id,
            request.Code,
            request.Name,
            request.Description,
            request.Status,
            (target, code, name, description, status) =>
            {
                target.Code = code;
                target.Name = name;
                target.Description = description;
                target.Status = status;
            });
        return MapUserGroup(entity);
    }

    /// <summary>
    /// zh-cn: 软删除用户组主数据；该操作不级联清理通用关联表，维护人员可以在关联维护视图中单独调整授权关系。
    /// en-us: Soft-deletes user-group master data without cascading generic association cleanup; maintainers can adjust grants separately in the association maintenance view.
    /// </summary>
    public Task<bool> DeleteUserGroupAsync(string id)
    {
        return _userGroupDomain.DeleteAsync(id);
    }

    public async Task<IReadOnlyList<RoleGroupManagementResult>> ListRoleGroupsAsync()
    {
        return (await _roleGroupDomain.ListAsync()).OrderBy(entity => entity.Code, StringComparer.Ordinal).Select(MapRoleGroup).ToList();
    }

    public async Task<RoleGroupManagementResult> UpsertRoleGroupAsync(UpsertRoleGroupRequest request)
    {
        var entity = await UpsertCodeNameEntityAsync(
            _roleGroupDomain,
            request.Id,
            request.Code,
            request.Name,
            request.Description,
            request.Status,
            (target, code, name, description, status) =>
            {
                target.Code = code;
                target.Name = name;
                target.Description = description;
                target.Status = status;
            });
        return MapRoleGroup(entity);
    }

    /// <summary>
    /// zh-cn: 软删除角色组主数据；角色组关联历史保留在通用关联表中，避免破坏授权审计轨迹。
    /// en-us: Soft-deletes role-group master data while keeping role-group association history in the generic association table to preserve authorization audit trails.
    /// </summary>
    public Task<bool> DeleteRoleGroupAsync(string id)
    {
        return _roleGroupDomain.DeleteAsync(id);
    }

    public async Task<IReadOnlyList<DepartmentManagementResult>> ListDepartmentsAsync()
    {
        return (await _departmentDomain.ListAsync()).OrderBy(entity => entity.Sort).Select(MapDepartment).ToList();
    }

    public async Task<DepartmentManagementResult> UpsertDepartmentAsync(UpsertDepartmentRequest request)
    {
        ValidateRequired(request.Code, "Department code is required.");
        ValidateRequired(request.Name, "Department name is required.");
        var entity = string.IsNullOrWhiteSpace(request.Id) ? new DepartmentEntity() : await _departmentDomain.GetAsync(request.Id) ?? new DepartmentEntity();
        entity.ParentId = NormalizeOptional(request.ParentId);
        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.Sort = request.Sort;
        entity.Status = request.Status;
        entity.IsDelete = 0;
        if (string.IsNullOrWhiteSpace(request.Id)) await _departmentDomain.CreateAsync(entity);
        else await _departmentDomain.UpdateAsync(entity);
        return MapDepartment(entity);
    }

    /// <summary>
    /// zh-cn: 软删除部门主数据；不会自动处理子部门或用户部门关联，避免维护操作产生隐式级联影响。
    /// en-us: Soft-deletes department master data without automatically processing child departments or user-department links, avoiding implicit cascade effects during maintenance.
    /// </summary>
    public Task<bool> DeleteDepartmentAsync(string id)
    {
        return _departmentDomain.DeleteAsync(id);
    }

    public async Task<IReadOnlyList<AssignmentManagementResult>> ListAssignmentsAsync()
    {
        return (await _assignmentDomain.ListAsync()).OrderBy(entity => entity.Code, StringComparer.Ordinal).Select(MapAssignment).ToList();
    }

    public async Task<AssignmentManagementResult> UpsertAssignmentAsync(UpsertAssignmentRequest request)
    {
        var entity = await UpsertCodeNameEntityAsync(
            _assignmentDomain,
            request.Id,
            request.Code,
            request.Name,
            request.Description,
            request.Status,
            (target, code, name, description, status) =>
            {
                target.Code = code;
                target.Name = name;
                target.Description = description;
                target.Status = status;
            });
        return MapAssignment(entity);
    }

    /// <summary>
    /// zh-cn: 软删除岗位主数据；用户岗位关系保留在通用关联表中，可由维护人员后续清理或恢复。
    /// en-us: Soft-deletes assignment master data while user-assignment links remain in the generic association table for later cleanup or restoration by maintainers.
    /// </summary>
    public Task<bool> DeleteAssignmentAsync(string id)
    {
        return _assignmentDomain.DeleteAsync(id);
    }

    /// <summary>
    /// <para>zh-cn:查询字典类型主数据，排除软删除记录，并按 Sort、Code 排序，保证管理端列表和下拉选项顺序稳定。</para>
    /// <para>en-us:Lists dictionary type master data excluding soft-deleted rows and orders by Sort then Code so management lists and selectors remain stable.</para>
    /// </summary>
    public async Task<IReadOnlyList<DictionaryTypeManagementResult>> ListDictionaryTypesAsync()
    {
        return (await _dictionaryTypeDomain.ListAsync())
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.Code, StringComparer.Ordinal)
            .Select(MapDictionaryType)
            .ToList();
    }

    /// <summary>
    /// <para>zh-cn:新增或更新字典类型；Code 和 Name 必填，Code 在全部字典类型中大小写不敏感唯一，更新软删除记录时会恢复 IsDelete 状态。</para>
    /// <para>en-us:Creates or updates a dictionary type; Code and Name are required, Code is case-insensitively unique across all types, and updates restore soft-deleted rows.</para>
    /// </summary>
    public async Task<DictionaryTypeManagementResult> UpsertDictionaryTypeAsync(UpsertDictionaryTypeRequest request)
    {
        var entity = await UpsertCodeNameEntityAsync(
            _dictionaryTypeDomain,
            request.Id,
            request.Code,
            request.Name,
            request.Description,
            request.Status,
            (target, code, name, description, status) =>
            {
                target.Code = code;
                target.Name = name;
                target.Description = description;
                target.Sort = request.Sort;
                target.Status = status;
            });
        return MapDictionaryType(entity);
    }

    /// <summary>
    /// <para>zh-cn:软删除字典类型，并软删除其下所有未删除字典项；如果类型不存在则按领域删除语义返回成功，不额外触发条目查询。</para>
    /// <para>en-us:Soft-deletes a dictionary type and all active items under it; when the type does not exist, the domain delete semantics are returned without querying items.</para>
    /// </summary>
    public async Task<bool> DeleteDictionaryTypeAsync(string id)
    {
        var dictionaryType = await _dictionaryTypeDomain.GetAsync(id);
        var deleted = await _dictionaryTypeDomain.DeleteAsync(id);
        if (!deleted || dictionaryType is null)
        {
            return deleted;
        }

        var items = await _dictionaryItemDomain.ListAsync(entity => entity.DictionaryTypeId == id);
        foreach (var item in items)
        {
            await _dictionaryItemDomain.DeleteAsync(item.Id);
        }

        return true;
    }

    /// <summary>
    /// <para>zh-cn:查询字典项；可按 DictionaryTypeId 过滤，返回结果按字典类型、Sort、Code 排序，适合维护页和业务下拉复用。</para>
    /// <para>en-us:Lists dictionary items, optionally filtered by DictionaryTypeId, and orders by type, Sort, and Code for reuse by maintenance pages and business selectors.</para>
    /// </summary>
    public async Task<IReadOnlyList<DictionaryItemManagementResult>> ListDictionaryItemsAsync(string? dictionaryTypeId = null)
    {
        var normalizedTypeId = NormalizeOptional(dictionaryTypeId);
        var items = string.IsNullOrWhiteSpace(normalizedTypeId)
            ? await _dictionaryItemDomain.ListAsync()
            : await _dictionaryItemDomain.ListAsync(entity => entity.DictionaryTypeId == normalizedTypeId);
        return items
            .OrderBy(entity => entity.DictionaryTypeId, StringComparer.Ordinal)
            .ThenBy(entity => entity.Sort)
            .ThenBy(entity => entity.Code, StringComparer.Ordinal)
            .Select(MapDictionaryItem)
            .ToList();
    }

    /// <summary>
    /// <para>zh-cn:新增或更新字典项；校验归属字典类型存在，Code 和 Name 必填，并确保同一字典类型下 Code 大小写不敏感唯一。</para>
    /// <para>en-us:Creates or updates a dictionary item; it validates the parent type exists, requires Code and Name, and enforces case-insensitive Code uniqueness within the same type.</para>
    /// </summary>
    public async Task<DictionaryItemManagementResult> UpsertDictionaryItemAsync(UpsertDictionaryItemRequest request)
    {
        ValidateRequired(request.DictionaryTypeId, "Dictionary type is required.");
        ValidateRequired(request.Code, "Dictionary item code is required.");
        ValidateRequired(request.Name, "Dictionary item name is required.");

        var dictionaryTypeId = request.DictionaryTypeId.Trim();
        if (await _dictionaryTypeDomain.GetAsync(dictionaryTypeId) is null)
        {
            throw new InvalidOperationException("Dictionary type does not exist.");
        }

        var normalizedCode = request.Code.Trim();
        var duplicate = (await _dictionaryItemDomain.ListIncludingDeletedAsync(entity => entity.DictionaryTypeId == dictionaryTypeId))
            .FirstOrDefault(entity =>
                string.Equals(entity.Code, normalizedCode, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(request.Id) || entity.Id != request.Id));
        if (duplicate is not null)
        {
            throw new InvalidOperationException("Dictionary item code already exists.");
        }

        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? new DictionaryItemEntity()
            : await _dictionaryItemDomain.GetAsync(request.Id) ?? new DictionaryItemEntity();
        entity.DictionaryTypeId = dictionaryTypeId;
        entity.Code = normalizedCode;
        entity.Name = request.Name.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.Sort = request.Sort;
        entity.Status = request.Status;
        entity.IsDelete = 0;

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            await _dictionaryItemDomain.CreateAsync(entity);
        }
        else
        {
            await _dictionaryItemDomain.UpdateAsync(entity);
        }

        return MapDictionaryItem(entity);
    }

    /// <summary>
    /// <para>zh-cn:软删除指定字典项；仅修改该条目删除标记，不会影响字典类型或同类型其他条目。</para>
    /// <para>en-us:Soft-deletes the specified dictionary item by marking only that row, without changing its parent type or sibling entries.</para>
    /// </summary>
    public Task<bool> DeleteDictionaryItemAsync(string id)
    {
        return _dictionaryItemDomain.DeleteAsync(id);
    }

    /// <summary>
    /// <para>zh-cn:查询运行环境主数据，并按项目、端、模块、排序和编码组织返回，支撑测试环境和部署信息维护页面。</para>
    /// <para>en-us:Lists runtime environment master data filtered and ordered by project, endpoint, module, sort, and code for the test-environment and deployment-info maintenance page.</para>
    /// </summary>
    public async Task<IReadOnlyList<RuntimeEnvironmentManagementResult>> ListRuntimeEnvironmentsAsync(
        string? projectId = null,
        string? endpointId = null,
        string? moduleId = null)
    {
        var normalizedProjectId = NormalizeOptional(projectId);
        var normalizedEndpointId = NormalizeOptional(endpointId);
        var normalizedModuleId = NormalizeOptional(moduleId);
        var environments = await _runtimeEnvironmentDomain.ListAsync(entity =>
            (normalizedProjectId == null || entity.ProjectId == normalizedProjectId) &&
            (normalizedEndpointId == null || entity.EndpointId == normalizedEndpointId) &&
            (normalizedModuleId == null || entity.ModuleId == normalizedModuleId));

        return environments
            .OrderBy(entity => entity.ProjectId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(entity => entity.EndpointId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(entity => entity.ModuleId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(entity => entity.Sort)
            .ThenBy(entity => entity.Code, StringComparer.Ordinal)
            .Select(MapRuntimeEnvironment)
            .ToList();
    }

    /// <summary>
    /// <para>zh-cn:新增或更新运行环境，拆分维护前端、API、MCP、部署目录、Compose 文件和本地发布包路径等字段。</para>
    /// <para>en-us:Creates or updates a runtime environment with separated fields for frontend, API, MCP, deployment directories, compose file, and local package paths.</para>
    /// </summary>
    public async Task<RuntimeEnvironmentManagementResult> UpsertRuntimeEnvironmentAsync(
        UpsertRuntimeEnvironmentRequest request)
    {
        ValidateRequired(request.Code, "Runtime environment code is required.");
        ValidateRequired(request.Name, "Runtime environment name is required.");
        ValidateRequired(request.EnvironmentType, "Runtime environment type is required.");

        var normalizedCode = request.Code.Trim();
        var normalizedProjectId = NormalizeOptional(request.ProjectId);
        var duplicate = (await _runtimeEnvironmentDomain.ListIncludingDeletedAsync(entity => entity.ProjectId == normalizedProjectId))
            .FirstOrDefault(entity =>
                string.Equals(entity.Code, normalizedCode, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(request.Id) || entity.Id != request.Id));
        if (duplicate is not null)
        {
            throw new InvalidOperationException("Runtime environment code already exists in the project.");
        }

        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? new RuntimeEnvironmentEntity()
            : await _runtimeEnvironmentDomain.GetAsync(request.Id) ?? new RuntimeEnvironmentEntity();
        entity.ProjectId = normalizedProjectId;
        entity.EndpointId = NormalizeOptional(request.EndpointId);
        entity.ModuleId = NormalizeOptional(request.ModuleId);
        entity.Code = normalizedCode;
        entity.Name = request.Name.Trim();
        entity.EnvironmentType = request.EnvironmentType.Trim().ToLowerInvariant();
        entity.Description = NormalizeOptional(request.Description);
        entity.FrontendUrl = NormalizeOptional(request.FrontendUrl);
        entity.ApiBaseUrl = NormalizeOptional(request.ApiBaseUrl);
        entity.FrontendProxyApiUrl = NormalizeOptional(request.FrontendProxyApiUrl);
        entity.McpEndpoint = NormalizeOptional(request.McpEndpoint);
        entity.DeployRoot = NormalizeOptional(request.DeployRoot);
        entity.DockerDirectory = NormalizeOptional(request.DockerDirectory);
        entity.RemotePackagePath = NormalizeOptional(request.RemotePackagePath);
        entity.ComposeFilePath = NormalizeOptional(request.ComposeFilePath);
        entity.LocalPackagePaths = NormalizeOptional(request.LocalPackagePaths);
        entity.Sort = request.Sort;
        entity.Status = request.Status;
        entity.IsDelete = 0;

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            await _runtimeEnvironmentDomain.CreateAsync(entity);
        }
        else
        {
            await _runtimeEnvironmentDomain.UpdateAsync(entity);
        }

        return MapRuntimeEnvironment(entity);
    }

    /// <summary>
    /// <para>zh-cn:软删除运行环境，并同步软删除其容器映射，保证环境主数据与部署明细生命周期一致。</para>
    /// <para>en-us:Soft-deletes a runtime environment and its container mappings so environment master data and deployment details share the same lifecycle.</para>
    /// </summary>
    public async Task<bool> DeleteRuntimeEnvironmentAsync(string id)
    {
        var environment = await _runtimeEnvironmentDomain.GetAsync(id);
        var deleted = await _runtimeEnvironmentDomain.DeleteAsync(id);
        if (!deleted || environment is null)
        {
            return deleted;
        }

        var containers = await _runtimeEnvironmentContainerDomain.ListAsync(entity => entity.RuntimeEnvironmentId == id);
        foreach (var container in containers)
        {
            await _runtimeEnvironmentContainerDomain.DeleteAsync(container.Id);
        }

        return true;
    }

    /// <summary>
    /// <para>zh-cn:查询运行环境下的容器映射，返回容器名称、宿主端口、容器端口和协议。</para>
    /// <para>en-us:Lists container mappings under a runtime environment, including container name, host port, container port, and protocol.</para>
    /// </summary>
    public async Task<IReadOnlyList<RuntimeEnvironmentContainerManagementResult>> ListRuntimeEnvironmentContainersAsync(
        string runtimeEnvironmentId)
    {
        ValidateRequired(runtimeEnvironmentId, "Runtime environment id is required.");
        return (await _runtimeEnvironmentContainerDomain.ListAsync(entity => entity.RuntimeEnvironmentId == runtimeEnvironmentId.Trim()))
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.Name, StringComparer.Ordinal)
            .Select(MapRuntimeEnvironmentContainer)
            .ToList();
    }

    /// <summary>
    /// <para>zh-cn:新增或更新容器端口映射，校验父运行环境存在、端口范围有效且同一环境内容器名称不重复。</para>
    /// <para>en-us:Creates or updates a container port mapping after validating the parent environment, port ranges, and name uniqueness within that environment.</para>
    /// </summary>
    public async Task<RuntimeEnvironmentContainerManagementResult> UpsertRuntimeEnvironmentContainerAsync(
        UpsertRuntimeEnvironmentContainerRequest request)
    {
        ValidateRequired(request.RuntimeEnvironmentId, "Runtime environment id is required.");
        ValidateRequired(request.Name, "Container name is required.");
        ValidatePort(request.HostPort, "Host port is invalid.");
        ValidatePort(request.ContainerPort, "Container port is invalid.");

        var runtimeEnvironmentId = request.RuntimeEnvironmentId.Trim();
        if (await _runtimeEnvironmentDomain.GetAsync(runtimeEnvironmentId) is null)
        {
            throw new InvalidOperationException("Runtime environment does not exist.");
        }

        var normalizedName = request.Name.Trim();
        var duplicate = (await _runtimeEnvironmentContainerDomain.ListIncludingDeletedAsync(entity =>
                entity.RuntimeEnvironmentId == runtimeEnvironmentId))
            .FirstOrDefault(entity =>
                string.Equals(entity.Name, normalizedName, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(request.Id) || entity.Id != request.Id));
        if (duplicate is not null)
        {
            throw new InvalidOperationException("Container name already exists in the runtime environment.");
        }

        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? new RuntimeEnvironmentContainerEntity()
            : await _runtimeEnvironmentContainerDomain.GetAsync(request.Id) ?? new RuntimeEnvironmentContainerEntity();
        entity.RuntimeEnvironmentId = runtimeEnvironmentId;
        entity.Name = normalizedName;
        entity.HostPort = request.HostPort;
        entity.ContainerPort = request.ContainerPort;
        entity.Protocol = NormalizeOptional(request.Protocol)?.ToLowerInvariant() ?? "tcp";
        entity.Description = NormalizeOptional(request.Description);
        entity.Sort = request.Sort;
        entity.Status = request.Status;
        entity.IsDelete = 0;

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            await _runtimeEnvironmentContainerDomain.CreateAsync(entity);
        }
        else
        {
            await _runtimeEnvironmentContainerDomain.UpdateAsync(entity);
        }

        return MapRuntimeEnvironmentContainer(entity);
    }

    /// <summary>
    /// <para>zh-cn:软删除单条容器映射，适用于调整部署端口而不删除整个运行环境的场景。</para>
    /// <para>en-us:Soft-deletes one container mapping for deployment-port adjustments without deleting the whole runtime environment.</para>
    /// </summary>
    public Task<bool> DeleteRuntimeEnvironmentContainerAsync(string id)
    {
        return _runtimeEnvironmentContainerDomain.DeleteAsync(id);
    }

    /// <summary>
    /// <para>zh-cn:查询固定的 Codex 提示词模板；只返回 MCP 接入和任务推进两个模板，传入其他环境时返回空集合。</para>
    /// <para>en-us:Lists fixed Codex prompt templates; only the MCP setup and task execution templates are returned, while other environments return an empty collection.</para>
    /// </summary>
    public async Task<IReadOnlyList<PromptTemplateManagementResult>> ListPromptTemplatesAsync(
        string? agentEnvironment = null)
    {
        var normalizedEnvironment = NormalizeAgentEnvironment(agentEnvironment);
        if (!IsSupportedAgentEnvironment(normalizedEnvironment))
        {
            return [];
        }

        return (await _promptTemplateDomain.ListAsync(entity =>
                entity.AgentEnvironment == normalizedEnvironment &&
                FixedPromptTemplateCodes.Contains(entity.Code)))
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.Code, StringComparer.Ordinal)
            .Select(MapPromptTemplate)
            .ToList();
    }

    /// <summary>
    /// <para>zh-cn:更新固定的 Codex 提示词模板，拒绝尚未适配的 Agent 环境和非固定模板编码以避免新增任意提示词。</para>
    /// <para>en-us:Updates a fixed Codex prompt template and rejects unsupported agent environments or non-fixed template codes to prevent arbitrary prompt creation.</para>
    /// </summary>
    public async Task<PromptTemplateManagementResult> UpsertPromptTemplateAsync(UpsertPromptTemplateRequest request)
    {
        var normalizedEnvironment = NormalizeAgentEnvironment(request.AgentEnvironment);
        if (!IsSupportedAgentEnvironment(normalizedEnvironment))
        {
            throw new InvalidOperationException("Only Codex prompt templates are supported currently.");
        }

        ValidateRequired(request.Code, "Prompt template code is required.");
        ValidateRequired(request.Name, "Prompt template name is required.");
        ValidateRequired(request.Content, "Prompt template content is required.");

        var normalizedCode = request.Code.Trim();
        if (!FixedPromptTemplateCodes.Contains(normalizedCode))
        {
            throw new InvalidOperationException("Only fixed Codex prompt templates can be maintained.");
        }

        var duplicate = (await _promptTemplateDomain.ListIncludingDeletedAsync(entity =>
                entity.AgentEnvironment == normalizedEnvironment))
            .FirstOrDefault(entity =>
                string.Equals(entity.Code, normalizedCode, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(request.Id) || entity.Id != request.Id));
        if (duplicate is not null)
        {
            throw new InvalidOperationException("Prompt template code already exists in the agent environment.");
        }

        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? new PromptTemplateEntity()
            : await _promptTemplateDomain.GetAsync(request.Id) ?? new PromptTemplateEntity();
        entity.AgentEnvironment = normalizedEnvironment;
        entity.Code = normalizedCode;
        entity.Name = request.Name.Trim();
        entity.Content = request.Content.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.Sort = request.Sort;
        entity.Status = request.Status;
        entity.IsDelete = 0;

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            await _promptTemplateDomain.CreateAsync(entity);
        }
        else
        {
            await _promptTemplateDomain.UpdateAsync(entity);
        }

        return MapPromptTemplate(entity);
    }

    /// <summary>
    /// <para>zh-cn:软删除提示词模板，仅影响指定模板记录。</para>
    /// <para>en-us:Soft-deletes a prompt template, affecting only the specified template record.</para>
    /// </summary>
    public Task<bool> DeletePromptTemplateAsync(string id)
    {
        return _promptTemplateDomain.DeleteAsync(id);
    }

    public async Task<IReadOnlyList<SecurityAssociationResult>> ListAssociationsAsync()
    {
        return (await _associationDomain.ListAsync()).Select(MapAssociation).ToList();
    }

    public async Task<SecurityAssociationResult> CreateAssociationAsync(SecurityAssociationRequest request)
    {
        ValidateRequired(request.SourceEntityId, "Source entity id is required.");
        ValidateRequired(request.TargetEntityId, "Target entity id is required.");
        ValidateRequired(request.AssociationType, "Association type is required.");
        await EnsureAssociationAsync(request.SourceEntityId, request.TargetEntityId, request.AssociationType);
        return (await ListAssociationsAsync()).First(item =>
            item.SourceEntityId == request.SourceEntityId &&
            item.TargetEntityId == request.TargetEntityId &&
            item.AssociationType == request.AssociationType);
    }

    public Task<bool> DeleteAssociationAsync(string id)
    {
        return _associationDomain.DeleteAsync(id);
    }

    private async Task<IReadOnlyList<string>> GetTargets(string sourceId, string associationType)
    {
        var targets = (await _associationDomain.ListAsync(entity =>
                entity.SourceEntityId == sourceId && entity.AssociationType == associationType))
            .Select(entity => entity.TargetEntityId)
            .ToList();
        if (targets.Count > 0)
        {
            return targets;
        }

        return associationType switch
        {
            SecurityAssociationTypes.UserRole => (await _userRoleDomain.ListAsync(entity => entity.UserId == sourceId)).Select(entity => entity.RoleId).ToList(),
            SecurityAssociationTypes.RoleMenu => (await _roleMenuDomain.ListAsync(entity => entity.RoleId == sourceId)).Select(entity => entity.MenuId).ToList(),
            SecurityAssociationTypes.RolePermission => (await _rolePermissionDomain.ListAsync(entity => entity.RoleId == sourceId)).Select(entity => entity.PermissionId).ToList(),
            _ => []
        };
    }

    private async Task ReplaceAssociationsAsync(string sourceId, string associationType, IReadOnlyList<string> targetIds)
    {
        var existing = await _associationDomain.ListIncludingDeletedAsync(entity =>
            entity.SourceEntityId == sourceId && entity.AssociationType == associationType);
        var wanted = targetIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToHashSet(StringComparer.Ordinal);

        foreach (var relation in existing)
        {
            relation.IsDelete = wanted.Contains(relation.TargetEntityId) ? 0 : 1;
            await _associationDomain.UpdateAsync(relation);
            wanted.Remove(relation.TargetEntityId);
        }

        foreach (var targetId in wanted)
        {
            await _associationDomain.CreateAsync(new EntityAssociationEntity
            {
                SourceEntityId = sourceId,
                TargetEntityId = targetId,
                AssociationType = associationType
            });
        }
    }

    private async Task EnsureAssociationAsync(string sourceId, string targetId, string associationType)
    {
        var existing = (await _associationDomain.ListIncludingDeletedAsync(entity =>
                entity.SourceEntityId == sourceId &&
                entity.TargetEntityId == targetId &&
                entity.AssociationType == associationType))
            .FirstOrDefault();
        if (existing is not null)
        {
            existing.IsDelete = 0;
            await _associationDomain.UpdateAsync(existing);
            return;
        }

        await _associationDomain.CreateAsync(new EntityAssociationEntity
        {
            SourceEntityId = sourceId,
            TargetEntityId = targetId,
            AssociationType = associationType
        });
    }

    private async Task ReplaceUserRolesAsync(string userId, IReadOnlyList<string> roleIds)
    {
        await ReplaceLegacyRelationsAsync(_userRoleDomain, relation => relation.UserId == userId, roleIds, roleId => new UserRoleEntity { UserId = userId, RoleId = roleId }, relation => relation.RoleId);
    }

    private async Task ReplaceRoleMenusAsync(string roleId, IReadOnlyList<string> menuIds)
    {
        await ReplaceLegacyRelationsAsync(_roleMenuDomain, relation => relation.RoleId == roleId, menuIds, menuId => new RoleMenuEntity { RoleId = roleId, MenuId = menuId }, relation => relation.MenuId);
    }

    private async Task ReplaceRolePermissionsAsync(string roleId, IReadOnlyList<string> permissionIds)
    {
        await ReplaceLegacyRelationsAsync(_rolePermissionDomain, relation => relation.RoleId == roleId, permissionIds, permissionId => new RolePermissionEntity { RoleId = roleId, PermissionId = permissionId }, relation => relation.PermissionId);
    }

    private static async Task ReplaceLegacyRelationsAsync<TEntity>(
        IEntityDomainBase<TEntity> domain,
        System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate,
        IReadOnlyList<string> targetIds,
        Func<string, TEntity> create,
        Func<TEntity, string> getTargetId)
        where TEntity : Model.Modules.Common.EntityBase, new()
    {
        var existing = await domain.ListIncludingDeletedAsync(predicate);
        var wanted = targetIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToHashSet(StringComparer.Ordinal);
        foreach (var relation in existing)
        {
            relation.IsDelete = wanted.Contains(getTargetId(relation)) ? 0 : 1;
            await domain.UpdateAsync(relation);
            wanted.Remove(getTargetId(relation));
        }

        foreach (var targetId in wanted)
        {
            await domain.CreateAsync(create(targetId));
        }
    }

    private static async Task<TEntity> UpsertCodeNameEntityAsync<TEntity>(
        IEntityDomainBase<TEntity> domain,
        string? id,
        string code,
        string name,
        string? description,
        int status,
        Action<TEntity, string, string, string?, int> assign)
        where TEntity : Model.Modules.Common.EntityBase, new()
    {
        ValidateRequired(code, "Code is required.");
        ValidateRequired(name, "Name is required.");
        var normalizedCode = code.Trim();
        var duplicate = (await domain.ListIncludingDeletedAsync())
            .FirstOrDefault(entity =>
                string.Equals(ReadCode(entity), normalizedCode, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(id) || entity.Id != id));
        if (duplicate is not null)
        {
            throw new InvalidOperationException("Code already exists.");
        }

        var entity = string.IsNullOrWhiteSpace(id) ? new TEntity() : await domain.GetAsync(id) ?? new TEntity();
        assign(entity, normalizedCode, name.Trim(), NormalizeOptional(description), status);
        entity.IsDelete = 0;
        if (string.IsNullOrWhiteSpace(id)) await domain.CreateAsync(entity);
        else await domain.UpdateAsync(entity);
        return entity;
    }

    private static string ReadCode<TEntity>(TEntity entity)
    {
        return entity switch
        {
            UserGroupEntity typed => typed.Code,
            RoleGroupEntity typed => typed.Code,
            AssignmentEntity typed => typed.Code,
            DictionaryTypeEntity typed => typed.Code,
            _ => string.Empty
        };
    }

    private static void ValidateRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeAgentEnvironment(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "codex" : value.Trim().ToLowerInvariant();
    }

    private static bool IsSupportedAgentEnvironment(string value)
    {
        return string.Equals(value, "codex", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidatePort(int value, string message)
    {
        if (value is < 1 or > 65535)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static MenuManagementResult MapMenu(MenuEntity entity)
    {
        return new MenuManagementResult(entity.Id, entity.ParentId, entity.Path, entity.Name, entity.Component, entity.Icon, entity.Sort, entity.Type, entity.Status);
    }

    private static PermissionManagementResult MapPermission(PermissionEntity entity)
    {
        return new PermissionManagementResult(entity.Id, entity.Code, entity.Name, entity.MenuId);
    }

    private static UserGroupManagementResult MapUserGroup(UserGroupEntity entity)
    {
        return new UserGroupManagementResult(entity.Id, entity.Code, entity.Name, entity.Description, entity.Status);
    }

    private static RoleGroupManagementResult MapRoleGroup(RoleGroupEntity entity)
    {
        return new RoleGroupManagementResult(entity.Id, entity.Code, entity.Name, entity.Description, entity.Status);
    }

    private static DepartmentManagementResult MapDepartment(DepartmentEntity entity)
    {
        return new DepartmentManagementResult(entity.Id, entity.ParentId, entity.Code, entity.Name, entity.Sort, entity.Status);
    }

    private static AssignmentManagementResult MapAssignment(AssignmentEntity entity)
    {
        return new AssignmentManagementResult(entity.Id, entity.Code, entity.Name, entity.Description, entity.Status);
    }

    private static DictionaryTypeManagementResult MapDictionaryType(DictionaryTypeEntity entity)
    {
        return new DictionaryTypeManagementResult(entity.Id, entity.Code, entity.Name, entity.Description, entity.Sort, entity.Status);
    }

    private static DictionaryItemManagementResult MapDictionaryItem(DictionaryItemEntity entity)
    {
        return new DictionaryItemManagementResult(entity.Id, entity.DictionaryTypeId, entity.Code, entity.Name, entity.Description, entity.Sort, entity.Status);
    }

    private static RuntimeEnvironmentManagementResult MapRuntimeEnvironment(RuntimeEnvironmentEntity entity)
    {
        return new RuntimeEnvironmentManagementResult(
            entity.Id,
            entity.ProjectId,
            entity.EndpointId,
            entity.ModuleId,
            entity.Code,
            entity.Name,
            entity.EnvironmentType,
            entity.Description,
            entity.FrontendUrl,
            entity.ApiBaseUrl,
            entity.FrontendProxyApiUrl,
            entity.McpEndpoint,
            entity.DeployRoot,
            entity.DockerDirectory,
            entity.RemotePackagePath,
            entity.ComposeFilePath,
            entity.LocalPackagePaths,
            entity.Sort,
            entity.Status);
    }

    private static RuntimeEnvironmentContainerManagementResult MapRuntimeEnvironmentContainer(
        RuntimeEnvironmentContainerEntity entity)
    {
        return new RuntimeEnvironmentContainerManagementResult(
            entity.Id,
            entity.RuntimeEnvironmentId,
            entity.Name,
            entity.HostPort,
            entity.ContainerPort,
            entity.Protocol,
            entity.Description,
            entity.Sort,
            entity.Status);
    }

    private static PromptTemplateManagementResult MapPromptTemplate(PromptTemplateEntity entity)
    {
        return new PromptTemplateManagementResult(
            entity.Id,
            entity.AgentEnvironment,
            entity.Code,
            entity.Name,
            entity.Content,
            entity.Description,
            entity.Sort,
            entity.Status);
    }

    private static SecurityAssociationResult MapAssociation(EntityAssociationEntity entity)
    {
        return new SecurityAssociationResult(entity.Id, entity.SourceEntityId, entity.TargetEntityId, entity.AssociationType);
    }
}
