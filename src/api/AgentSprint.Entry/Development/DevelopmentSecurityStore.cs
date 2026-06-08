using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Service.Security;
using AgentSprint.Service.Services.AuthServices;
using AgentSprint.Service.Services.SecurityServices;
using AgentSprint.Service.Services.UserServices;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgentSprint.Entry.Development;

public sealed class DevelopmentUserDomain : IUserDomain
{
    /// <summary>
    /// zh-cn: 基于开发账号目录创建只读用户领域对象，保证启用开发登录时令牌归属、令牌列表和 MCP 换取身份使用同一套用户标识。
    /// en-us: Creates a read-only user domain from the development account catalog so token ownership, token lists, and MCP identity exchange use the same user ids when development sign-in is enabled.
    /// </summary>
    public DevelopmentUserDomain()
    {
    }

    public Task<string> CreateAsync(UserEntity entity)
    {
        return Task.FromException<string>(new NotSupportedException("Development users are read-only."));
    }

    public Task<UserEntity?> GetAsync(string id)
    {
        return Task.FromResult(ToEntity(DevelopmentUsers.FindById(id)));
    }

    public Task<IList<UserEntity>> ListAsync(System.Linq.Expressions.Expression<Func<UserEntity, bool>>? predicate = null)
    {
        IQueryable<UserEntity> query = DevelopmentUsers.All.Select(ToEntity).Where(user => user is not null).Cast<UserEntity>().AsQueryable();
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return Task.FromResult<IList<UserEntity>>(query.ToList());
    }

    public Task<IList<UserEntity>> ListIncludingDeletedAsync(System.Linq.Expressions.Expression<Func<UserEntity, bool>>? predicate = null)
    {
        return ListAsync(predicate);
    }

    public Task<string> UpdateAsync(UserEntity entity)
    {
        return Task.FromException<string>(new NotSupportedException("Development users are read-only."));
    }

    public Task<bool> DeleteAsync(string id)
    {
        return Task.FromException<bool>(new NotSupportedException("Development users are read-only."));
    }

    public Task<UserEntity?> FindByUsernameAsync(string username)
    {
        return Task.FromResult(ToEntity(DevelopmentUsers.FindByUsername(username)));
    }

    public Task<UserEntity?> FindAnyByUsernameAsync(string username)
    {
        return FindByUsernameAsync(username);
    }

    private static UserEntity? ToEntity(DevelopmentUser? user)
    {
        return user is null
            ? null
            : new UserEntity
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Avatar = user.Avatar,
                Status = 1
            };
    }
}

public sealed class DevelopmentAuthService : IAuthService
{
    private readonly JwtOptions _jwtOptions;

    public DevelopmentAuthService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// zh-cn: 使用固定开发账号完成本地登录，所有开发账号统一使用 123456 密码，并按开发用户目录中的角色签发 JWT。
    /// en-us: Signs in with a fixed local-development account. Every development account uses the 123456 password, and the issued JWT contains roles from the shared development user catalog.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 登录请求，Username 必须匹配开发用户目录，Password 必须为 123456。
    /// en-us: Login request whose Username must match the development user catalog and whose Password must be 123456.
    /// </param>
    /// <returns>
    /// zh-cn: 登录结果，包含访问令牌、用户资料、角色集合和默认首页。
    /// en-us: Login result containing the access token, user profile, role collection, and default home path.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">
    /// zh-cn: 用户名不存在或密码不匹配时抛出。
    /// en-us: Thrown when the username is missing from the catalog or the password does not match.
    /// </exception>
    public Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var user = DevelopmentUsers.FindByUsername(request.Username);
        if (user is null || request.Password != DevelopmentUsers.Password)
        {
            throw new UnauthorizedAccessException("Username or password is incorrect.");
        }

        var token = CreateAccessToken(user.Id, user.Username, user.Roles);
        return Task.FromResult(new LoginResult(
            token,
            user.Id,
            user.Username,
            user.DisplayName,
            user.Avatar,
            user.Roles,
            "/dashboard/workspace"));
    }

    public Task<IReadOnlyList<string>> GetAccessCodesAsync(string userId)
    {
        return Task.FromResult<IReadOnlyList<string>>(["system:all"]);
    }

    public string CreateAccessToken(string userId, string username, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, username)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class DevelopmentUserService : IUserService
{
    public Task<CurrentUserResult> GetCurrentUserAsync(string userId)
    {
        var user = DevelopmentUsers.FindById(userId) ?? DevelopmentUsers.Admin;
        return Task.FromResult(new CurrentUserResult(
            user.Id,
            user.Id,
            user.Username,
            user.DisplayName,
            user.Avatar,
            user.Roles,
            "/dashboard/workspace",
            string.Empty,
            string.Empty));
    }

    public Task<IReadOnlyList<UserOptionResult>> ListUserOptionsAsync()
    {
        return Task.FromResult<IReadOnlyList<UserOptionResult>>(
            DevelopmentUsers.All
                .Select(user => new UserOptionResult(user.Id, user.Username, user.DisplayName))
                .ToList());
    }

    public Task<IReadOnlyList<MenuResult>> GetMenusAsync(string userId)
    {
        IReadOnlyList<MenuResult> menus =
        [
            CreateMenu("Workspace", "/dashboard/workspace", "/dashboard/workspace/index", "lucide:panel-top", 0, "工作台", affixTab: true),
            CreateGroupMenu("ProjectGroup", "/sprint/project", "项目管理", "lucide:folder-kanban", 10, "/sprint/projects",
            [
                CreateMenu("SprintProjects", "/sprint/projects", "/sprint/projects/index", "lucide:folder-kanban", 10, "项目配置", affixTab: true),
                CreateMenu("SprintMultiEndpoints", "/sprint/multi-endpoints", "/sprint/multi-endpoints/index", "lucide:layout-dashboard", 11, "多端管理", affixTab: true),
                CreateMenu("SprintSkills", "/sprint/skills", "/sprint/skills/index", "lucide:brain-circuit", 12, "Skill配置", affixTab: true),
                CreateMenu("SprintProjectDetail", "/sprint/projects/detail/:id", "/sprint/projects/detail", null, 13, "项目详情", true, "/sprint/projects")
            ]),
            CreateGroupMenu("ProductGroup", "/sprint/product", "产品管理", "lucide:list-checks", 20, "/sprint/requirements",
            [
                CreateMenu("SprintRequirements", "/sprint/requirements", "/sprint/requirements/index", "lucide:list-checks", 10, "需求管理"),
                CreateMenu("SprintRequirementDetail", "/sprint/requirements/detail/:id", "/sprint/requirements/detail", null, 11, "需求详情", true, "/sprint/requirements"),
                CreateMenu("SprintRequirementReviews", "/sprint/reviews", "/sprint/reviews/index", "lucide:clipboard-check", 20, "需求评审")
            ]),
            CreateGroupMenu("WorkerGroup", "/sprint/worker", "研发执行", "lucide:workflow", 30, "/sprint/my-tasks",
            [
                CreateMenu("SprintMyTasks", "/sprint/my-tasks", "/sprint/my-tasks/index", "lucide:user-check", 10, "我的任务"),
                CreateMenu("SprintTasks", "/sprint/tasks", "/sprint/tasks/index", "lucide:layout-list", 20, "任务大厅"),
                CreateMenu("SprintTaskDetail", "/sprint/tasks/detail/:id", "/sprint/tasks/detail", null, 21, "任务详情", true, "/sprint/tasks")
            ]),
            CreateGroupMenu("TestGroup", "/sprint/test", "测试验证", "lucide:test-tube-2", 40, "/sprint/tests",
            [
                CreateMenu("SprintTests", "/sprint/tests", "/sprint/tests/index", "lucide:test-tube-2", 10, "测试计划"),
                CreateMenu("SprintDefects", "/sprint/defects", "/sprint/defects/index", "lucide:bug", 20, "缺陷跟踪"),
                CreateMenu("SprintDefectDetail", "/sprint/defects/detail/:id", "/sprint/defects/detail", null, 21, "缺陷详情", true, "/sprint/defects")
            ]),
            CreateGroupMenu("System", "/system", "系统管理", "lucide:settings", 90, "/system/users",
            [
                CreateMenu("SystemUsers", "/system/users", "/system/users/index", "lucide:users", 10, "用户管理"),
                CreateMenu("SystemRoles", "/system/roles", "/system/roles/index", "lucide:shield-check", 20, "角色管理"),
                CreateMenu("SystemMenus", "/system/menus", "/system/menus/index", "lucide:menu", 30, "菜单管理"),
                CreateMenu("SystemDictionaries", "/system/dictionaries", "/system/dictionaries/index", "lucide:book-open-text", 40, "字典管理"),
                CreateMenu("SystemConfigurations", "/system/configurations", "/system/configurations/index", "lucide:sliders-horizontal", 45, "系统配置"),
                CreateMenu("SystemDepartments", "/system/departments", "/system/departments/index", "lucide:network", 50, "部门管理"),
                CreateMenu("SystemAssignments", "/system/assignments", "/system/assignments/index", "lucide:briefcase-business", 60, "岗位管理")
            ]),
            CreateGroupMenu("GlobalConfig", "/global-config", "全局配置", "lucide:sliders-horizontal", 92, "/global-config/environments",
            [
                CreateMenu("GlobalConfigEnvironments", "/global-config/environments", "/system/runtime-environments/index", "lucide:server-cog", 10, "环境管理"),
                CreateMenu("GlobalConfigPromptTemplates", "/global-config/prompt-templates", "/system/prompt-templates/index", "lucide:message-square-code", 20, "提示词设置")
            ]),
            CreateGroupMenu("Security", "/security", "安全管理", "lucide:shield-check", 95, "/system/agent-tokens",
            [
                CreateMenu("SystemAgentTokens", "/system/agent-tokens", "/system/agent-tokens/index", "lucide:key-square", 10, "令牌管理")
            ])
        ];

        return Task.FromResult(menus);
    }

    private static MenuResult CreateGroupMenu(
        string name,
        string path,
        string title,
        string icon,
        int order,
        string redirect,
        List<MenuResult> children)
    {
        return new MenuResult
        {
            Name = name,
            Path = path,
            Redirect = redirect,
            Meta = new MenuMetaResult
            {
                Icon = icon,
                Order = order,
                Title = title
            },
            Children = children
        };
    }

    private static MenuResult CreateMenu(
        string name,
        string path,
        string component,
        string? icon,
        int order,
        string title,
        bool hideInMenu = false,
        string? activePath = null,
        bool affixTab = false)
    {
        return new MenuResult
        {
            Name = name,
            Path = path,
            Component = component,
            Meta = new MenuMetaResult
            {
                ActivePath = activePath,
                AffixTab = affixTab ? true : null,
                HideInMenu = hideInMenu ? true : null,
                Icon = icon,
                Order = order,
                Title = title
            }
        };
    }
}

internal static class DevelopmentUsers
{
    public const string Password = "123456";

    public static readonly DevelopmentUser Admin = new(
        "dev-admin",
        "admin",
        "Administrator",
        "https://unpkg.com/@vbenjs/static-source@0.1.7/source/avatar-v1.webp",
        ["super"]);

    public static readonly IReadOnlyList<DevelopmentUser> All =
    [
        Admin,
        new("pm-1", "pm", "Product Manager", null, ["pm"]),
        new("arch-1", "architect", "Architect", null, ["architect"]),
        new("manager-1", "project-manager", "Project Manager", null, ["project_manager"]),
        new("dev-1", "developer", "Developer", null, ["developer"]),
        new("tester-1", "tester", "Tester", null, ["tester"])
    ];

    public static DevelopmentUser? FindById(string? userId)
    {
        return All.SingleOrDefault(user => string.Equals(user.Id, userId, StringComparison.OrdinalIgnoreCase));
    }

    public static DevelopmentUser? FindByUsername(string? username)
    {
        return All.SingleOrDefault(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed record DevelopmentUser(
    string Id,
    string Username,
    string DisplayName,
    string? Avatar,
    IReadOnlyList<string> Roles);

public sealed class DevelopmentSystemManagementService : ISystemManagementService
{
    public Task<IReadOnlyList<UserManagementResult>> ListUsersAsync()
    {
        return Task.FromResult<IReadOnlyList<UserManagementResult>>(
            DevelopmentUsers.All
                .Select(user => new UserManagementResult(
                    user.Id,
                    user.Username,
                    user.DisplayName,
                    null,
                    null,
                    user.Avatar,
                    1,
                    user.Roles.ToList()))
                .ToList());
    }

    public Task<UserManagementResult> UpsertUserAsync(UpsertUserRequest request)
    {
        return Task.FromResult(new UserManagementResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.Username,
            request.DisplayName,
            request.Email,
            request.PhoneNumber,
            request.Avatar,
            request.Status,
            request.RoleIds ?? []));
    }

    public Task<bool> DeleteUserAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<RoleManagementResult>> ListRolesAsync()
    {
        IReadOnlyList<RoleManagementResult> roles =
        [
            new("role-super", "super", "Super Administrator", null, 1, DevelopmentMenus.All.Select(menu => menu.Id).ToList(), ["system:all"]),
            new("role-pm", "pm", "Product Manager", null, 1, [], []),
            new("role-architect", "architect", "Architect", null, 1, [], []),
            new("role-project-manager", "project_manager", "Project Manager", null, 1, [], []),
            new("role-developer", "developer", "Developer", null, 1, [], []),
            new("role-tester", "tester", "Tester", null, 1, [], [])
        ];
        return Task.FromResult(roles);
    }

    public Task<RoleManagementResult> UpsertRoleAsync(UpsertRoleRequest request)
    {
        return Task.FromResult(new RoleManagementResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.Code,
            request.Name,
            request.Description,
            request.Status,
            request.MenuIds ?? [],
            request.PermissionIds ?? []));
    }

    public Task<bool> DeleteRoleAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<MenuManagementResult>> ListMenusAsync()
    {
        return Task.FromResult<IReadOnlyList<MenuManagementResult>>(DevelopmentMenus.All);
    }

    public Task<MenuManagementResult> UpsertMenuAsync(UpsertMenuRequest request)
    {
        return Task.FromResult(new MenuManagementResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.ParentId,
            request.Path,
            request.Name,
            request.Component,
            request.Icon,
            request.Sort,
            request.Type,
            request.Status));
    }

    public Task<bool> DeleteMenuAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<PermissionManagementResult>> ListPermissionsAsync()
    {
        IReadOnlyList<PermissionManagementResult> permissions =
        [
            new("permission-all", "system:all", "All system permissions", null),
            new("permission-system-user", "System:User:Manage", "User management", "menu-system-users"),
            new("permission-system-role", "System:Role:Manage", "Role management", "menu-system-roles"),
            new("permission-system-menu", "System:Menu:Manage", "Menu management", "menu-system-menus"),
            new("permission-system-permission", "System:Permission:Manage", "Button permission management", "menu-system-menus"),
            new("permission-system-dictionary", "System:Dictionary:Manage", "Dictionary management", "menu-system-dictionaries"),
            new("permission-system-runtime-environment", "System:RuntimeEnvironment:Manage", "Runtime environment management", "menu-global-config-environments"),
            new("permission-system-prompt-template", "System:PromptTemplate:Manage", "Prompt template management", "menu-global-config-prompt-templates"),
            new("permission-system-configuration", "System:Configuration:Manage", "System configuration management", "menu-system-configurations"),
            new("permission-security-agent-token", "Security:AgentToken:Manage", "Agent token management", "menu-security-agent-tokens")
        ];
        return Task.FromResult(permissions);
    }

    public Task<PermissionManagementResult> UpsertPermissionAsync(UpsertPermissionRequest request)
    {
        return Task.FromResult(new PermissionManagementResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.Code,
            request.Name,
            request.MenuId));
    }

    public Task<bool> DeletePermissionAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<UserGroupManagementResult>> ListUserGroupsAsync() => Task.FromResult<IReadOnlyList<UserGroupManagementResult>>([]);

    public Task<UserGroupManagementResult> UpsertUserGroupAsync(UpsertUserGroupRequest request)
    {
        return Task.FromResult(new UserGroupManagementResult(request.Id ?? Guid.NewGuid().ToString("N"), request.Code, request.Name, request.Description, request.Status));
    }

    public Task<bool> DeleteUserGroupAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<RoleGroupManagementResult>> ListRoleGroupsAsync() => Task.FromResult<IReadOnlyList<RoleGroupManagementResult>>([]);

    public Task<RoleGroupManagementResult> UpsertRoleGroupAsync(UpsertRoleGroupRequest request)
    {
        return Task.FromResult(new RoleGroupManagementResult(request.Id ?? Guid.NewGuid().ToString("N"), request.Code, request.Name, request.Description, request.Status));
    }

    public Task<bool> DeleteRoleGroupAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<DepartmentManagementResult>> ListDepartmentsAsync() => Task.FromResult<IReadOnlyList<DepartmentManagementResult>>([]);

    public Task<DepartmentManagementResult> UpsertDepartmentAsync(UpsertDepartmentRequest request)
    {
        return Task.FromResult(new DepartmentManagementResult(request.Id ?? Guid.NewGuid().ToString("N"), request.ParentId, request.Code, request.Name, request.Sort, request.Status));
    }

    public Task<bool> DeleteDepartmentAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<AssignmentManagementResult>> ListAssignmentsAsync() => Task.FromResult<IReadOnlyList<AssignmentManagementResult>>([]);

    public Task<AssignmentManagementResult> UpsertAssignmentAsync(UpsertAssignmentRequest request)
    {
        return Task.FromResult(new AssignmentManagementResult(request.Id ?? Guid.NewGuid().ToString("N"), request.Code, request.Name, request.Description, request.Status));
    }

    public Task<bool> DeleteAssignmentAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<DictionaryTypeManagementResult>> ListDictionaryTypesAsync()
    {
        IReadOnlyList<DictionaryTypeManagementResult> dictionaryTypes =
        [
            new("dict-type-requirement-priority", "requirement_priority", "需求优先级", "需求排期与任务拆解使用的优先级字典。", 10, 1),
            new("dict-type-common-status", "common_status", "通用状态", "系统主数据通用启停状态。", 20, 1)
        ];
        return Task.FromResult(dictionaryTypes);
    }

    public Task<DictionaryTypeManagementResult> UpsertDictionaryTypeAsync(UpsertDictionaryTypeRequest request)
    {
        return Task.FromResult(new DictionaryTypeManagementResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.Code,
            request.Name,
            request.Description,
            request.Sort,
            request.Status));
    }

    public Task<bool> DeleteDictionaryTypeAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<DictionaryItemManagementResult>> ListDictionaryItemsAsync(string? dictionaryTypeId = null)
    {
        IReadOnlyList<DictionaryItemManagementResult> dictionaryItems =
        [
            new("dict-item-priority-high", "dict-type-requirement-priority", "high", "高", "需要优先交付。", 10, 1),
            new("dict-item-priority-medium", "dict-type-requirement-priority", "medium", "中", "正常排期交付。", 20, 1),
            new("dict-item-status-enabled", "dict-type-common-status", "enabled", "启用", null, 10, 1),
            new("dict-item-status-disabled", "dict-type-common-status", "disabled", "停用", null, 20, 1)
        ];
        if (!string.IsNullOrWhiteSpace(dictionaryTypeId))
        {
            dictionaryItems = dictionaryItems.Where(item => item.DictionaryTypeId == dictionaryTypeId).ToList();
        }

        return Task.FromResult(dictionaryItems);
    }

    public Task<DictionaryItemManagementResult> UpsertDictionaryItemAsync(UpsertDictionaryItemRequest request)
    {
        return Task.FromResult(new DictionaryItemManagementResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.DictionaryTypeId,
            request.Code,
            request.Name,
            request.Description,
            request.Sort,
            request.Status));
    }

    public Task<bool> DeleteDictionaryItemAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<RuntimeEnvironmentManagementResult>> ListRuntimeEnvironmentsAsync(
        string? projectId = null,
        string? endpointId = null,
        string? moduleId = null)
    {
        IReadOnlyList<RuntimeEnvironmentManagementResult> environments =
        [
            new(
                "runtime-env-test",
                projectId,
                endpointId,
                moduleId,
                "test",
                "测试环境",
                "test",
                "AgentSprint 默认测试环境",
                "http://192.168.80.101:5999",
                "http://192.168.80.101:5000",
                "http://192.168.80.101:5999/api",
                "http://192.168.80.101:5010/mcp",
                "/opt/agentsprint-deploy",
                "/opt/agentsprint-deploy/docker",
                "/opt/agentsprint-deploy/agentsprint-docker-deploy.tgz",
                "/opt/agentsprint-deploy/docker/docker-compose.yml",
                string.Join(Environment.NewLine, @"F:\AI\AgentSprint\agentsprint-docker-deploy.tar", @"F:\AI\AgentSprint\agentsprint-docker-deploy.tgz", @"F:\AI\AgentSprint\agentsprint-docker-deploy.tar.zip"),
                10,
                1)
        ];
        return Task.FromResult(environments);
    }

    public Task<RuntimeEnvironmentManagementResult> UpsertRuntimeEnvironmentAsync(UpsertRuntimeEnvironmentRequest request)
    {
        return Task.FromResult(new RuntimeEnvironmentManagementResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.ProjectId,
            request.EndpointId,
            request.ModuleId,
            request.Code,
            request.Name,
            request.EnvironmentType,
            request.Description,
            request.FrontendUrl,
            request.ApiBaseUrl,
            request.FrontendProxyApiUrl,
            request.McpEndpoint,
            request.DeployRoot,
            request.DockerDirectory,
            request.RemotePackagePath,
            request.ComposeFilePath,
            request.LocalPackagePaths,
            request.Sort,
            request.Status));
    }

    public Task<bool> DeleteRuntimeEnvironmentAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<RuntimeEnvironmentContainerManagementResult>> ListRuntimeEnvironmentContainersAsync(
        string runtimeEnvironmentId)
    {
        IReadOnlyList<RuntimeEnvironmentContainerManagementResult> containers =
        [
            new("runtime-container-admin", runtimeEnvironmentId, "agentsprint-admin", 5999, 80, "tcp", null, 10, 1),
            new("runtime-container-api", runtimeEnvironmentId, "agentsprint-api", 5000, 5000, "tcp", null, 20, 1),
            new("runtime-container-mcp", runtimeEnvironmentId, "agentsprint-mcp", 5010, 5010, "tcp", null, 30, 1)
        ];
        return Task.FromResult(containers);
    }

    public Task<RuntimeEnvironmentContainerManagementResult> UpsertRuntimeEnvironmentContainerAsync(
        UpsertRuntimeEnvironmentContainerRequest request)
    {
        return Task.FromResult(new RuntimeEnvironmentContainerManagementResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.RuntimeEnvironmentId,
            request.Name,
            request.HostPort,
            request.ContainerPort,
            request.Protocol ?? "tcp",
            request.Description,
            request.Sort,
            request.Status));
    }

    public Task<bool> DeleteRuntimeEnvironmentContainerAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<PromptTemplateManagementResult>> ListPromptTemplatesAsync(string? agentEnvironment = null)
    {
        IReadOnlyList<PromptTemplateManagementResult> templates = string.Equals(agentEnvironment ?? "codex", "codex", StringComparison.OrdinalIgnoreCase)
            ?
            [
                new(
                    "prompt-codex-mcp-setup",
                    "codex",
                    "mcp_setup",
                    "MCP 接入提示词",
                    "请按 AgentSprint MCP 接入说明配置 Codex HTTP MCP，并确保请求头使用 http_headers。",
                    "Codex agentsprint MCP 接入配置提示词。",
                    10,
                    1),
                new(
                    "prompt-codex-task-execution",
                    "codex",
                    "task_execution",
                    "Codex 任务执行提示词",
                    "请读取任务上下文、按项目规范实现变更、运行相关测试，并在完成后报告修改点与验证命令。",
                    "Codex 环境默认任务执行提示词模板。",
                    20,
                    1)
            ]
            : [];
        return Task.FromResult(templates);
    }

    public Task<PromptTemplateManagementResult> UpsertPromptTemplateAsync(UpsertPromptTemplateRequest request)
    {
        return Task.FromResult(new PromptTemplateManagementResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.AgentEnvironment,
            request.Code,
            request.Name,
            request.Content,
            request.Description,
            request.Sort,
            request.Status));
    }

    public Task<bool> DeletePromptTemplateAsync(string id) => Task.FromResult(true);

    public Task<IReadOnlyList<SecurityAssociationResult>> ListAssociationsAsync() => Task.FromResult<IReadOnlyList<SecurityAssociationResult>>([]);

    public Task<SecurityAssociationResult> CreateAssociationAsync(SecurityAssociationRequest request)
    {
        return Task.FromResult(new SecurityAssociationResult(Guid.NewGuid().ToString("N"), request.SourceEntityId, request.TargetEntityId, request.AssociationType));
    }

    public Task<bool> DeleteAssociationAsync(string id) => Task.FromResult(true);
}

internal static class DevelopmentMenus
{
    public static readonly IReadOnlyList<MenuManagementResult> All =
    [
        new("menu-dashboard-workspace", null, "/dashboard/workspace", "Workspace", "/dashboard/workspace/index", "lucide:panel-top", 0, 1, 1),
        new("menu-sprint-project", null, "/sprint/project", "ProjectGroup", null, "lucide:folder-kanban", 10, 0, 1),
        new("menu-sprint-projects", "menu-sprint-project", "/sprint/projects", "SprintProjects", "/sprint/projects/index", "lucide:folder-kanban", 10, 1, 1),
        new("menu-sprint-multi-endpoints", "menu-sprint-project", "/sprint/multi-endpoints", "SprintMultiEndpoints", "/sprint/multi-endpoints/index", "lucide:layout-dashboard", 11, 1, 1),
        new("menu-sprint-skills", "menu-sprint-project", "/sprint/skills", "SprintSkills", "/sprint/skills/index", "lucide:brain-circuit", 12, 1, 1),
        new("menu-sprint-project-detail", "menu-sprint-project", "/sprint/projects/detail/:id", "SprintProjectDetail", "/sprint/projects/detail", null, 13, 2, 1),
        new("menu-sprint-product", null, "/sprint/product", "ProductGroup", null, "lucide:list-checks", 20, 0, 1),
        new("menu-sprint-requirements", "menu-sprint-product", "/sprint/requirements", "SprintRequirements", "/sprint/requirements/index", "lucide:list-checks", 10, 1, 1),
        new("menu-sprint-requirement-detail", "menu-sprint-product", "/sprint/requirements/detail/:id", "SprintRequirementDetail", "/sprint/requirements/detail", null, 11, 2, 1),
        new("menu-sprint-reviews", "menu-sprint-product", "/sprint/reviews", "SprintRequirementReviews", "/sprint/reviews/index", "lucide:clipboard-check", 20, 1, 1),
        new("menu-sprint-worker", null, "/sprint/worker", "WorkerGroup", null, "lucide:workflow", 30, 0, 1),
        new("menu-sprint-my-tasks", "menu-sprint-worker", "/sprint/my-tasks", "SprintMyTasks", "/sprint/my-tasks/index", "lucide:user-check", 10, 1, 1),
        new("menu-sprint-tasks", "menu-sprint-worker", "/sprint/tasks", "SprintTasks", "/sprint/tasks/index", "lucide:layout-list", 20, 1, 1),
        new("menu-sprint-task-detail", "menu-sprint-worker", "/sprint/tasks/detail/:id", "SprintTaskDetail", "/sprint/tasks/detail", null, 21, 2, 1),
        new("menu-sprint-test", null, "/sprint/test", "TestGroup", null, "lucide:test-tube-2", 40, 0, 1),
        new("menu-sprint-tests", "menu-sprint-test", "/sprint/tests", "SprintTests", "/sprint/tests/index", "lucide:test-tube-2", 10, 1, 1),
        new("menu-sprint-defects", "menu-sprint-test", "/sprint/defects", "SprintDefects", "/sprint/defects/index", "lucide:bug", 20, 1, 1),
        new("menu-sprint-defect-detail", "menu-sprint-test", "/sprint/defects/detail/:id", "SprintDefectDetail", "/sprint/defects/detail", null, 21, 2, 1),
        new("menu-system", null, "/system", "System", null, "lucide:settings", 90, 0, 1),
        new("menu-system-users", "menu-system", "/system/users", "SystemUsers", "/system/users/index", "lucide:users", 10, 1, 1),
        new("menu-system-roles", "menu-system", "/system/roles", "SystemRoles", "/system/roles/index", "lucide:shield-check", 20, 1, 1),
        new("menu-system-menus", "menu-system", "/system/menus", "SystemMenus", "/system/menus/index", "lucide:menu", 30, 1, 1),
        new("menu-system-dictionaries", "menu-system", "/system/dictionaries", "SystemDictionaries", "/system/dictionaries/index", "lucide:book-open-text", 40, 1, 1),
        new("menu-system-configurations", "menu-system", "/system/configurations", "SystemConfigurations", "/system/configurations/index", "lucide:sliders-horizontal", 45, 1, 1),
        new("menu-system-departments", "menu-system", "/system/departments", "SystemDepartments", "/system/departments/index", "lucide:network", 50, 1, 1),
        new("menu-system-assignments", "menu-system", "/system/assignments", "SystemAssignments", "/system/assignments/index", "lucide:briefcase-business", 60, 1, 1),
        new("menu-global-config", null, "/global-config", "GlobalConfig", null, "lucide:sliders-horizontal", 92, 0, 1),
        new("menu-global-config-environments", "menu-global-config", "/global-config/environments", "GlobalConfigEnvironments", "/system/runtime-environments/index", "lucide:server-cog", 10, 1, 1),
        new("menu-global-config-prompt-templates", "menu-global-config", "/global-config/prompt-templates", "GlobalConfigPromptTemplates", "/system/prompt-templates/index", "lucide:message-square-code", 20, 1, 1),
        new("menu-security", null, "/security", "Security", null, "lucide:shield-check", 95, 0, 1),
        new("menu-security-agent-tokens", "menu-security", "/system/agent-tokens", "SystemAgentTokens", "/system/agent-tokens/index", "lucide:key-square", 10, 1, 1)
    ];
}
