using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.SecurityServices;

namespace AgentSprint.Service.Impls.SecurityServices;

public sealed class SecurityAuthorizationService : AgentSprintServiceBase, ISecurityAuthorizationService
{
    private static readonly HashSet<string> SprintMenuNames =
    [
        "Workspace",
        "ProjectGroup",
        "SprintProjects",
        "SprintSkills",
        "SprintMultiEndpoints",
        "SprintProjectDetail",
        "ProductGroup",
        "SprintRequirements",
        "SprintRequirementDetail",
        "SprintRequirementReviews",
        "WorkerGroup",
        "SprintTasks",
        "SprintTaskDetail",
        "SprintMyTasks",
        "TestGroup",
        "SprintTests",
        "SprintDefects",
        "SprintDefectDetail",
        "System",
        "SystemUsers",
        "SystemRoles",
        "SystemMenus",
        "SystemConfigurations",
        "SystemDepartments",
        "SystemAssignments",
        "Security",
        "SystemAgentTokens"
    ];

    private readonly IRoleDomain _roleDomain;
    private readonly IMenuDomain _menuDomain;
    private readonly IPermissionDomain _permissionDomain;
    private readonly IUserRoleDomain _userRoleDomain;
    private readonly IRoleMenuDomain _roleMenuDomain;
    private readonly IRolePermissionDomain _rolePermissionDomain;
    private readonly IEntityAssociationDomain _entityAssociationDomain;

    /// <summary>
    /// zh-cn: 创建通用授权解析服务，优先读取通用关联表，并在关联数据尚未完整迁移时回退到旧 RBAC 关系表。
    /// en-us: Creates the common authorization resolver. It reads the generic association table first and falls back to legacy RBAC relation tables while migration is incomplete.
    /// </summary>
    public SecurityAuthorizationService(
        IRoleDomain roleDomain,
        IMenuDomain menuDomain,
        IPermissionDomain permissionDomain,
        IUserRoleDomain userRoleDomain,
        IRoleMenuDomain roleMenuDomain,
        IRolePermissionDomain rolePermissionDomain,
        IEntityAssociationDomain entityAssociationDomain)
    {
        _roleDomain = roleDomain;
        _menuDomain = menuDomain;
        _permissionDomain = permissionDomain;
        _userRoleDomain = userRoleDomain;
        _roleMenuDomain = roleMenuDomain;
        _rolePermissionDomain = rolePermissionDomain;
        _entityAssociationDomain = entityAssociationDomain;
    }

    /// <summary>
    /// zh-cn: 解析用户最终角色编号，覆盖直接角色、用户组授权角色、用户组授权角色组、用户直接角色组和角色组包含角色；无通用关联结果时回退旧 UserRole。
    /// en-us: Resolves final role ids for a user, covering direct user-role links, user-group role grants, user-group role-group grants, direct user-role-group grants, and role-group contained roles; falls back to legacy UserRole when generic associations produce no result.
    /// </summary>
    /// <param name="userId">
    /// zh-cn: 当前用户编号。
    /// en-us: Current user id.
    /// </param>
    /// <returns>
    /// zh-cn: 去重并排序后的角色编号集合。
    /// en-us: Distinct and ordered role id collection.
    /// </returns>
    public async Task<IReadOnlyList<string>> ResolveRoleIdsAsync(string userId)
    {
        var associations = await _entityAssociationDomain.ListAsync();
        var roleIds = ResolveRoleIdsFromAssociations(userId, associations);
        if (roleIds.Count == 0)
        {
            roleIds.UnionWith((await _userRoleDomain.ListAsync(entity => entity.UserId == userId))
                .Select(entity => entity.RoleId));
        }

        return roleIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// zh-cn: 解析用户最终角色编码，仅返回启用角色。
    /// en-us: Resolves final role codes for a user and returns only active roles.
    /// </summary>
    /// <param name="userId">
    /// zh-cn: 当前用户编号。
    /// en-us: Current user id.
    /// </param>
    /// <returns>
    /// zh-cn: 启用角色编码集合，无角色时返回空集合。
    /// en-us: Active role code collection, or an empty collection when no role is granted.
    /// </returns>
    public async Task<IReadOnlyList<string>> ResolveRoleCodesAsync(string userId)
    {
        var roleIds = (await ResolveRoleIdsAsync(userId)).ToHashSet(StringComparer.Ordinal);
        if (roleIds.Count == 0)
        {
            return [];
        }

        return (await _roleDomain.ListAsync(entity => roleIds.Contains(entity.Id) && entity.Status == 1))
            .Select(entity => entity.Code)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// zh-cn: 解析用户权限码，优先使用通用关联表中的角色-权限关系；未配置时回退旧 RolePermission 关系。
    /// en-us: Resolves permission codes for a user, preferring role-permission associations in the generic association table and falling back to legacy RolePermission links when not configured.
    /// </summary>
    /// <param name="userId">
    /// zh-cn: 当前用户编号。
    /// en-us: Current user id.
    /// </param>
    /// <returns>
    /// zh-cn: 权限码集合，无授权时返回空集合。
    /// en-us: Permission code collection, or an empty collection when no permission is granted.
    /// </returns>
    public async Task<IReadOnlyList<string>> ResolvePermissionCodesAsync(string userId)
    {
        var roleIds = (await ResolveRoleIdsAsync(userId)).ToHashSet(StringComparer.Ordinal);
        if (roleIds.Count == 0)
        {
            return [];
        }

        var permissionIds = (await _entityAssociationDomain.ListAsync(entity =>
                roleIds.Contains(entity.SourceEntityId) &&
                entity.AssociationType == SecurityAssociationTypes.RolePermission))
            .Select(entity => entity.TargetEntityId)
            .ToHashSet(StringComparer.Ordinal);

        if (permissionIds.Count == 0)
        {
            permissionIds = (await _rolePermissionDomain.ListAsync(entity => roleIds.Contains(entity.RoleId)))
                .Select(entity => entity.PermissionId)
                .ToHashSet(StringComparer.Ordinal);
        }

        return permissionIds.Count == 0
            ? []
            : (await _permissionDomain.ListAsync(entity => permissionIds.Contains(entity.Id)))
                .Select(entity => entity.Code)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToList();
    }

    /// <summary>
    /// zh-cn: 解析用户菜单树，优先使用通用关联表中的角色-菜单关系；菜单 Type 为 2 时作为隐藏路由返回。
    /// en-us: Resolves a user's menu tree, preferring role-menu associations in the generic association table; menu records with Type 2 are returned as hidden routes.
    /// </summary>
    /// <param name="userId">
    /// zh-cn: 当前用户编号。
    /// en-us: Current user id.
    /// </param>
    /// <returns>
    /// zh-cn: 后端动态菜单树，过滤旧演示、概览、关于等非当前业务菜单。
    /// en-us: Backend-driven menu tree filtered to the current business scope and excluding legacy demo/dashboard/about menus.
    /// </returns>
    public async Task<IReadOnlyList<MenuResult>> ResolveMenusAsync(string userId)
    {
        var roleIds = (await ResolveRoleIdsAsync(userId)).ToHashSet(StringComparer.Ordinal);
        if (roleIds.Count == 0)
        {
            return [];
        }

        var menuIds = (await _entityAssociationDomain.ListAsync(entity =>
                roleIds.Contains(entity.SourceEntityId) &&
                entity.AssociationType == SecurityAssociationTypes.RoleMenu))
            .Select(entity => entity.TargetEntityId)
            .ToHashSet(StringComparer.Ordinal);

        if (menuIds.Count == 0)
        {
            menuIds = (await _roleMenuDomain.ListAsync(entity => roleIds.Contains(entity.RoleId)))
                .Select(entity => entity.MenuId)
                .ToHashSet(StringComparer.Ordinal);
        }

        if (menuIds.Count == 0)
        {
            return [];
        }

        var menus = (await _menuDomain.ListAsync(entity => menuIds.Contains(entity.Id) && entity.Status == 1))
            .Where(entity => !IsRemovedMenu(entity.Name, entity.Path))
            .OrderBy(entity => entity.Sort)
            .Select(entity => new MenuTreeItem(
                entity.Id,
                entity.ParentId,
                new MenuResult
                {
                    Path = entity.Path,
                    Name = entity.Name,
                    Component = entity.Component,
                    Redirect = ResolveRedirect(entity.Name),
                    Meta = new MenuMetaResult
                    {
                        Icon = entity.Icon,
                        Order = entity.Sort,
                        Title = ResolveMenuTitle(entity.Name),
                        HideInMenu = entity.Type == 2 ? true : null,
                        ActivePath = ResolveActivePath(entity.Name),
                        AffixTab = entity.Name is "Workspace" or "SprintProjects" or "SprintSkills" or "SprintMultiEndpoints" ? true : null
                    }
                }))
            .ToList();

        return BuildTree(menus);
    }

    /// <summary>
    /// zh-cn: 从通用关联集合中计算用户可获得的角色编号；该方法不访问数据库，便于授权链路复用和单元测试。
    /// en-us: Computes role ids granted to a user from generic associations without database access so the authorization chain can be reused and unit tested.
    /// </summary>
    /// <param name="userId">
    /// zh-cn: 需要解析授权链路的用户编号。
    /// en-us: User id whose authorization chain should be resolved.
    /// </param>
    /// <param name="associations">
    /// zh-cn: 通用关联集合，调用方应传入未软删除的有效关联。
    /// en-us: Generic association collection; callers should pass active associations that are not soft-deleted.
    /// </param>
    /// <returns>
    /// zh-cn: 去重后的角色编号集合。
    /// en-us: Distinct role id set.
    /// </returns>
    public static HashSet<string> ResolveRoleIdsFromAssociations(
        string userId,
        IEnumerable<EntityAssociationEntity> associations)
    {
        var directRoleIds = associations
            .Where(entity => entity.SourceEntityId == userId && entity.AssociationType == SecurityAssociationTypes.UserRole)
            .Select(entity => entity.TargetEntityId);
        var userGroupIds = associations
            .Where(entity => entity.SourceEntityId == userId && entity.AssociationType == SecurityAssociationTypes.UserUserGroup)
            .Select(entity => entity.TargetEntityId)
            .ToHashSet(StringComparer.Ordinal);
        var userRoleGroupIds = associations
            .Where(entity => entity.SourceEntityId == userId && entity.AssociationType == SecurityAssociationTypes.UserRoleGroup)
            .Select(entity => entity.TargetEntityId)
            .ToHashSet(StringComparer.Ordinal);
        var groupRoleIds = associations
            .Where(entity => userGroupIds.Contains(entity.SourceEntityId) && entity.AssociationType == SecurityAssociationTypes.UserGroupRole)
            .Select(entity => entity.TargetEntityId);
        var groupRoleGroupIds = associations
            .Where(entity => userGroupIds.Contains(entity.SourceEntityId) && entity.AssociationType == SecurityAssociationTypes.UserGroupRoleGroup)
            .Select(entity => entity.TargetEntityId);

        userRoleGroupIds.UnionWith(groupRoleGroupIds);
        var roleGroupRoleIds = associations
            .Where(entity => userRoleGroupIds.Contains(entity.TargetEntityId) && entity.AssociationType == SecurityAssociationTypes.RoleRoleGroup)
            .Select(entity => entity.SourceEntityId);

        return directRoleIds
            .Concat(groupRoleIds)
            .Concat(roleGroupRoleIds)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsRemovedMenu(string name, string path)
    {
        if (path == "/sprint" ||
            path == "/system/org" ||
            path.StartsWith("/demos", StringComparison.Ordinal) ||
            path.StartsWith("/vben-admin", StringComparison.Ordinal))
        {
            return true;
        }

        return path.StartsWith("/sprint/", StringComparison.Ordinal) && !SprintMenuNames.Contains(name);
    }

    private static List<MenuResult> BuildTree(IReadOnlyList<MenuTreeItem> menus)
    {
        var lookup = menus.ToDictionary(menu => menu.Id, StringComparer.Ordinal);
        var roots = new List<MenuResult>();

        foreach (var item in menus)
        {
            if (!string.IsNullOrWhiteSpace(item.ParentId) && lookup.TryGetValue(item.ParentId, out var parent))
            {
                parent.Route.Children.Add(item.Route);
            }
            else
            {
                roots.Add(item.Route);
            }
        }

        return roots;
    }

    private static string ResolveMenuTitle(string name)
    {
        return name switch
        {
            "ProjectGroup" => "项目管理",
            "SprintProjects" => "项目配置",
            "SprintSkills" => "Skill配置",
            "SprintMultiEndpoints" => "多端管理",
            "SprintProjectDetail" => "项目详情",
            "ProductGroup" => "产品管理",
            "SprintRequirements" => "需求管理",
            "SprintRequirementDetail" => "需求详情",
            "SprintRequirementReviews" => "需求评审",
            "WorkerGroup" => "研发执行",
            "SprintTasks" => "任务大厅",
            "SprintTaskDetail" => "任务详情",
            "SprintMyTasks" => "我的任务",
            "TestGroup" => "测试验证",
            "SprintTests" => "测试计划",
            "SprintDefects" => "缺陷跟踪",
            "SprintDefectDetail" => "缺陷详情",
            "System" => "系统管理",
            "SystemUsers" => "用户管理",
            "SystemRoles" => "角色管理",
            "SystemMenus" => "菜单管理",
            "SystemConfigurations" => "系统配置",
            "SystemAgentTokens" => "令牌管理",
            "SystemDepartments" => "部门管理",
            "SystemAssignments" => "岗位管理",
            "Security" => "安全管理",
            "Workspace" => "工作台",
            _ => name
        };
    }

    private static string? ResolveRedirect(string name)
    {
        return name switch
        {
            "ProjectGroup" => "/sprint/projects",
            "ProductGroup" => "/sprint/requirements",
            "WorkerGroup" => "/sprint/my-tasks",
            "TestGroup" => "/sprint/tests",
            "System" => "/system/users",
            "Security" => "/system/agent-tokens",
            _ => null
        };
    }

    private static string? ResolveActivePath(string name)
    {
        return name switch
        {
            "SprintProjectDetail" => "/sprint/projects",
            "SprintRequirementDetail" => "/sprint/requirements",
            "SprintTaskDetail" => "/sprint/tasks",
            "SprintDefectDetail" => "/sprint/defects",
            _ => null
        };
    }

    private sealed record MenuTreeItem(string Id, string? ParentId, MenuResult Route);
}
