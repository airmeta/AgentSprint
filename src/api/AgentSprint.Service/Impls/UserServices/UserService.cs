using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services.SecurityServices;
using AgentSprint.Service.Services.UserServices;

namespace AgentSprint.Service.Impls.UserServices;

public sealed class UserService : IUserService
{
    private readonly IUserDomain _userDomain;
    private readonly ISecurityAuthorizationService _authorizationService;

    /// <summary>
    /// zh-cn: 创建用户服务，注入用户领域对象和通用授权解析服务，用于组装当前用户信息、选择器选项和后端动态菜单树。
    /// en-us: Creates the user service with the user domain and common authorization resolver so it can compose current-user information, selector options, and backend-driven menu trees.
    /// </summary>
    /// <param name="userDomain">
    /// zh-cn: 用户领域对象，用于读取登录用户基础资料。
    /// en-us: User domain used to read the signed-in user's profile.
    /// </param>
    /// <param name="authorizationService">
    /// zh-cn: 通用授权解析服务，负责从通用关联表和旧 RBAC 关系表解析角色与菜单。
    /// en-us: Common authorization resolver responsible for resolving roles and menus from generic associations and legacy RBAC relation tables.
    /// </param>
    public UserService(
        IUserDomain userDomain,
        ISecurityAuthorizationService authorizationService)
    {
        _userDomain = userDomain;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// zh-cn: 根据用户编号读取当前用户资料和角色编码。用户不存在时抛出异常；没有角色时返回空角色集合。
    /// en-us: Reads the current user's profile and role codes by user id. Throws when the user does not exist and returns an empty role collection when no roles are assigned.
    /// </summary>
    /// <param name="userId">
    /// zh-cn: 当前认证用户编号，必须能在用户表中找到。
    /// en-us: Authenticated user id that must exist in the user table.
    /// </param>
    /// <returns>
    /// zh-cn: 返回管理端当前用户模型，包含头像、角色和默认首页。
    /// en-us: Returns the admin current-user model including avatar, roles, and default home path.
    /// </returns>
    public async Task<CurrentUserResult> GetCurrentUserAsync(string userId)
    {
        var user = await _userDomain.GetAsync(userId) ?? throw new InvalidOperationException("User does not exist.");
        var roles = await _authorizationService.ResolveRoleCodesAsync(userId);

        return new CurrentUserResult(
            user.Id,
            user.Id,
            user.Username,
            user.DisplayName,
            user.Avatar,
            roles,
            "/dashboard/workspace",
            string.Empty,
            string.Empty);
    }

    /// <summary>
    /// zh-cn: 查询启用用户并转换为管理端选择器选项，供评审人选择和任务指派使用。
    /// en-us: Lists active users as admin selector options for reviewer selection and task assignment.
    /// </summary>
    /// <returns>
    /// zh-cn: 按显示名称和用户名排序的用户选项。
    /// en-us: User options ordered by display name and username.
    /// </returns>
    public async Task<IReadOnlyList<UserOptionResult>> ListUserOptionsAsync()
    {
        var users = await _userDomain.ListAsync(entity => entity.Status == 1);
        return users
            .OrderBy(entity => entity.DisplayName, StringComparer.Ordinal)
            .ThenBy(entity => entity.Username, StringComparer.Ordinal)
            .Select(entity => new UserOptionResult(entity.Id, entity.Username, entity.DisplayName))
            .ToList();
    }

    /// <summary>
    /// zh-cn: 根据用户角色授权读取后端动态菜单，并按 ParentId 组装树形结构；菜单 Type 为 2 的记录会作为隐藏详情路由返回，不显示在左侧菜单但可被路由访问。
    /// en-us: Reads backend-driven menus authorized by the user's roles and builds a tree by ParentId; records with menu Type 2 are returned as hidden detail routes that are routable without appearing in the sidebar menu.
    /// </summary>
    /// <param name="userId">
    /// zh-cn: 当前认证用户编号，用于查询用户角色和角色菜单关系。
    /// en-us: Authenticated user id used to query user-role and role-menu relations.
    /// </param>
    /// <returns>
    /// zh-cn: 返回可供 Vben 后端菜单模式生成路由和左侧菜单的菜单树；用户无角色或角色无菜单时返回空集合。
    /// en-us: Returns the menu tree used by Vben backend-menu mode to generate routes and sidebar menus; returns an empty collection when the user has no roles or no authorized menus.
    /// </returns>
    public async Task<IReadOnlyList<MenuResult>> GetMenusAsync(string userId)
    {
        return await _authorizationService.ResolveMenusAsync(userId);
    }
}
