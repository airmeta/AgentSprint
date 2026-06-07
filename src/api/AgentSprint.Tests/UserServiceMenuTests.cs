using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Service.Impls.SecurityServices;
using AgentSprint.Service.Impls.UserServices;

namespace AgentSprint.Tests;

public sealed class UserServiceMenuTests
{
    [Fact]
    public async Task GetMenusAsync_ReturnsSprintMenusAsRootPersonaGroupsAndHiddenDetailRoutes()
    {
        var user = new UserEntity { Id = "user-1", Username = "admin", DisplayName = "Administrator" };
        var role = new RoleEntity { Id = "role-1", Code = "super", Name = "Super Administrator" };
        var sprint = new MenuEntity
        {
            Id = "menu-sprint",
            Name = "Sprint",
            Path = "/sprint",
            Icon = "lucide:kanban",
            Sort = 30
        };
        var projectGroup = CreateMenu("menu-project-group", string.Empty, "ProjectGroup", "/sprint/project", string.Empty, 10, 0);
        var productGroup = CreateMenu("menu-product-group", string.Empty, "ProductGroup", "/sprint/product", string.Empty, 20, 0);
        var workerGroup = CreateMenu("menu-worker-group", string.Empty, "WorkerGroup", "/sprint/worker", string.Empty, 30, 0);
        var testGroup = CreateMenu("menu-test-group", string.Empty, "TestGroup", "/sprint/test", string.Empty, 40, 0);
        var projects = CreateMenu("menu-projects", projectGroup.Id, "SprintProjects", "/sprint/projects", "/sprint/projects/index", 10, 1);
        var multiEndpoints = CreateMenu("menu-multi-endpoints", projectGroup.Id, "SprintMultiEndpoints", "/sprint/multi-endpoints", "/sprint/multi-endpoints/index", 11, 1);
        var skills = CreateMenu("menu-skills", projectGroup.Id, "SprintSkills", "/sprint/skills", "/sprint/skills/index", 12, 1);
        var projectDetail = CreateMenu(
            "menu-project-detail",
            projectGroup.Id,
            "SprintProjectDetail",
            "/sprint/projects/detail/:id",
            "/sprint/projects/detail",
            13,
            2);
        var requirements = CreateMenu("menu-requirements", productGroup.Id, "SprintRequirements", "/sprint/requirements", "/sprint/requirements/index", 10, 1);
        var requirementDetail = CreateMenu(
            "menu-requirement-detail",
            productGroup.Id,
            "SprintRequirementDetail",
            "/sprint/requirements/detail/:id",
            "/sprint/requirements/detail",
            11,
            2);
        var reviews = CreateMenu("menu-reviews", productGroup.Id, "SprintRequirementReviews", "/sprint/reviews", "/sprint/reviews/index", 20, 1);
        var myTasks = CreateMenu("menu-my-tasks", workerGroup.Id, "SprintMyTasks", "/sprint/my-tasks", "/sprint/my-tasks/index", 10, 1);
        var tasks = CreateMenu("menu-tasks", workerGroup.Id, "SprintTasks", "/sprint/tasks", "/sprint/tasks/index", 20, 1);
        var taskDetail = CreateMenu(
            "menu-task-detail",
            workerGroup.Id,
            "SprintTaskDetail",
            "/sprint/tasks/detail/:id",
            "/sprint/tasks/detail",
            21,
            2);
        var tests = CreateMenu("menu-tests", testGroup.Id, "SprintTests", "/sprint/tests", "/sprint/tests/index", 10, 1);
        var defects = CreateMenu("menu-defects", testGroup.Id, "SprintDefects", "/sprint/defects", "/sprint/defects/index", 20, 1);
        var defectDetail = CreateMenu(
            "menu-defect-detail",
            testGroup.Id,
            "SprintDefectDetail",
            "/sprint/defects/detail/:id",
            "/sprint/defects/detail",
            21,
            2);
        var staleMvp = CreateMenu("menu-mvp", sprint.Id, "SprintMvp", "/sprint/mvp", "/sprint/mvp/index", 10, 1);
        var workspace = CreateMenu("menu-dashboard-workspace", string.Empty, "Workspace", "/dashboard/workspace", "/dashboard/workspace/index", 0, 1);
        var staleDecomposition = CreateMenu(
            "menu-decomposition",
            sprint.Id,
            "SprintTaskDecomposition",
            "/sprint/decomposition",
            "/sprint/decomposition/index",
            45,
            0);
        var userDomain = new InMemoryUserDomain([user]);
        var roleDomain = new InMemoryRoleDomain([role]);
        var userRoleDomain = new InMemoryUserRoleDomain([new UserRoleEntity { UserId = user.Id, RoleId = role.Id }]);
        var menuDomain = new InMemoryMenuDomain(
            [
                sprint,
                workspace,
                staleDecomposition,
                staleMvp,
                projectGroup,
                projects,
                multiEndpoints,
                skills,
                projectDetail,
                productGroup,
                requirements,
                requirementDetail,
                reviews,
                workerGroup,
                myTasks,
                tasks,
                taskDetail,
                testGroup,
                tests,
                defects,
                defectDetail
            ]);
        var roleMenuDomain = new InMemoryRoleMenuDomain(
            [
                new RoleMenuEntity { RoleId = role.Id, MenuId = sprint.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = workspace.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = staleMvp.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = staleDecomposition.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = projectGroup.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = projects.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = multiEndpoints.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = skills.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = projectDetail.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = productGroup.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = requirements.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = requirementDetail.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = reviews.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = workerGroup.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = myTasks.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = tasks.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = taskDetail.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = testGroup.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = tests.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = defects.Id },
                new RoleMenuEntity { RoleId = role.Id, MenuId = defectDetail.Id }
            ]);
        var service = new UserService(
            userDomain,
            CreateAuthorizationService(
                roleDomain,
                menuDomain,
                userRoleDomain,
                roleMenuDomain,
                new InMemoryRolePermissionDomain([]),
                new InMemoryPermissionDomain([]),
                new InMemoryEntityAssociationDomain([])));

        var menus = await service.GetMenusAsync(user.Id);

        Assert.Equal(["Workspace", "ProjectGroup", "ProductGroup", "WorkerGroup", "TestGroup"], menus.Select(menu => menu.Name));
        Assert.DoesNotContain(menus, menu => menu.Name == "Sprint");
        Assert.DoesNotContain(menus, menu => menu.Name.Contains("Decomposition", StringComparison.Ordinal));
        Assert.DoesNotContain(menus, menu => menu.Name == "SprintMvp");

        var visibleMenus = menus
            .Where(menu => menu.Meta.HideInMenu != true)
            .Select(menu => menu.Meta.Title)
            .ToList();
        Assert.Equal(["工作台", "项目管理", "产品管理", "研发执行", "测试验证"], visibleMenus);

        var workspaceMenu = menus.Single(menu => menu.Name == "Workspace");
        Assert.Equal("/dashboard/workspace", workspaceMenu.Path);
        Assert.Equal("/dashboard/workspace/index", workspaceMenu.Component);
        Assert.True(workspaceMenu.Meta.AffixTab);

        var projectGroupMenu = menus.Single(menu => menu.Name == "ProjectGroup");
        Assert.Equal("/sprint/projects", projectGroupMenu.Redirect);
        Assert.Equal(["SprintProjects", "SprintMultiEndpoints", "SprintSkills", "SprintProjectDetail"], projectGroupMenu.Children.Select(menu => menu.Name));

        var productGroupMenu = menus.Single(menu => menu.Name == "ProductGroup");
        Assert.Equal(["SprintRequirements", "SprintRequirementDetail", "SprintRequirementReviews"], productGroupMenu.Children.Select(menu => menu.Name));

        var workerGroupMenu = menus.Single(menu => menu.Name == "WorkerGroup");
        Assert.Equal(["SprintMyTasks", "SprintTasks", "SprintTaskDetail"], workerGroupMenu.Children.Select(menu => menu.Name));

        var testGroupMenu = menus.Single(menu => menu.Name == "TestGroup");
        Assert.Equal(["SprintTests", "SprintDefects", "SprintDefectDetail"], testGroupMenu.Children.Select(menu => menu.Name));

        var projectMenu = projectGroupMenu.Children.Single(menu => menu.Name == "SprintProjects");
        Assert.Equal("项目配置", projectMenu.Meta.Title);
        Assert.True(projectMenu.Meta.AffixTab);
        Assert.Null(projectMenu.Meta.HideInMenu);

        var multiEndpointMenu = projectGroupMenu.Children.Single(menu => menu.Name == "SprintMultiEndpoints");
        Assert.Equal("多端管理", multiEndpointMenu.Meta.Title);
        Assert.True(multiEndpointMenu.Meta.AffixTab);
        Assert.Null(multiEndpointMenu.Meta.HideInMenu);
        Assert.Equal("/sprint/multi-endpoints", multiEndpointMenu.Path);
        Assert.Equal("/sprint/multi-endpoints/index", multiEndpointMenu.Component);

        var skillMenu = projectGroupMenu.Children.Single(menu => menu.Name == "SprintSkills");
        Assert.Equal("Skill配置", skillMenu.Meta.Title);
        Assert.True(skillMenu.Meta.AffixTab);
        Assert.Null(skillMenu.Meta.HideInMenu);
        Assert.Equal("/sprint/skills", skillMenu.Path);
        Assert.Equal("/sprint/skills/index", skillMenu.Component);

        var detailMenu = projectGroupMenu.Children.Single(menu => menu.Name == "SprintProjectDetail");
        Assert.Equal("项目详情", detailMenu.Meta.Title);
        Assert.True(detailMenu.Meta.HideInMenu);
        Assert.Equal("/sprint/projects", detailMenu.Meta.ActivePath);
        Assert.Equal("/sprint/projects/detail/:id", detailMenu.Path);
        Assert.Equal("/sprint/projects/detail", detailMenu.Component);

        var requirementDetailMenu = productGroupMenu.Children.Single(menu => menu.Name == "SprintRequirementDetail");
        Assert.True(requirementDetailMenu.Meta.HideInMenu);
        Assert.Equal("/sprint/requirements", requirementDetailMenu.Meta.ActivePath);

        var taskDetailMenu = workerGroupMenu.Children.Single(menu => menu.Name == "SprintTaskDetail");
        Assert.True(taskDetailMenu.Meta.HideInMenu);
        Assert.Equal("/sprint/tasks", taskDetailMenu.Meta.ActivePath);

        var defectDetailMenu = testGroupMenu.Children.Single(menu => menu.Name == "SprintDefectDetail");
        Assert.True(defectDetailMenu.Meta.HideInMenu);
        Assert.Equal("/sprint/defects", defectDetailMenu.Meta.ActivePath);
    }

    [Fact]
    public async Task GetMenusAsync_ReturnsEmptyMenusWhenUserHasNoRoles()
    {
        var user = new UserEntity { Id = "user-1", Username = "admin", DisplayName = "Administrator" };
        var service = new UserService(
            new InMemoryUserDomain([user]),
            CreateAuthorizationService(
                new InMemoryRoleDomain([]),
                new InMemoryMenuDomain([]),
                new InMemoryUserRoleDomain([]),
                new InMemoryRoleMenuDomain([]),
                new InMemoryRolePermissionDomain([]),
                new InMemoryPermissionDomain([]),
                new InMemoryEntityAssociationDomain([])));

        var menus = await service.GetMenusAsync(user.Id);

        Assert.Empty(menus);
    }

    private static MenuEntity CreateMenu(
        string id,
        string parentId,
        string name,
        string path,
        string component,
        int sort,
        int type)
    {
        return new MenuEntity
        {
            Id = id,
            ParentId = string.IsNullOrWhiteSpace(parentId) ? null : parentId,
            Name = name,
            Path = path,
            Component = string.IsNullOrWhiteSpace(component) ? null : component,
            Sort = sort,
            Type = type,
            Status = 1
        };
    }

    private static SecurityAuthorizationService CreateAuthorizationService(
        IRoleDomain roleDomain,
        IMenuDomain menuDomain,
        IUserRoleDomain userRoleDomain,
        IRoleMenuDomain roleMenuDomain,
        IRolePermissionDomain rolePermissionDomain,
        IPermissionDomain permissionDomain,
        IEntityAssociationDomain entityAssociationDomain)
    {
        return new SecurityAuthorizationService(
            roleDomain,
            menuDomain,
            permissionDomain,
            userRoleDomain,
            roleMenuDomain,
            rolePermissionDomain,
            entityAssociationDomain);
    }
}

internal sealed class InMemoryUserDomain(IList<UserEntity> entities)
    : InMemorySecurityDomain<UserEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IUserDomain
{
    public Task<UserEntity?> FindByUsernameAsync(string username)
    {
        return Task.FromResult(
            Entities.SingleOrDefault(entity =>
                entity.IsDelete == 0 &&
                System.String.Equals(entity.Username, username, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<UserEntity?> FindAnyByUsernameAsync(string username)
    {
        return Task.FromResult(
            Entities.SingleOrDefault(entity =>
                System.String.Equals(entity.Username, username, StringComparison.OrdinalIgnoreCase)));
    }
}

internal sealed class InMemoryRoleDomain(IList<RoleEntity> entities)
    : InMemorySecurityDomain<RoleEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IRoleDomain
{
    public Task<RoleEntity?> FindAnyByCodeAsync(string code)
    {
        return Task.FromResult(
            Entities.SingleOrDefault(entity =>
                System.String.Equals(entity.Code, code, StringComparison.OrdinalIgnoreCase)));
    }
}

internal sealed class InMemoryUserRoleDomain(IList<UserRoleEntity> entities)
    : InMemorySecurityDomain<UserRoleEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IUserRoleDomain;

internal sealed class InMemoryMenuDomain(IList<MenuEntity> entities)
    : InMemorySecurityDomain<MenuEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IMenuDomain;

internal sealed class InMemoryRoleMenuDomain(IList<RoleMenuEntity> entities)
    : InMemorySecurityDomain<RoleMenuEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IRoleMenuDomain;

internal sealed class InMemoryPermissionDomain(IList<PermissionEntity> entities)
    : InMemorySecurityDomain<PermissionEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IPermissionDomain;

internal sealed class InMemoryRolePermissionDomain(IList<RolePermissionEntity> entities)
    : InMemorySecurityDomain<RolePermissionEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IRolePermissionDomain;

internal sealed class InMemoryEntityAssociationDomain(IList<EntityAssociationEntity> entities)
    : InMemorySecurityDomain<EntityAssociationEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IEntityAssociationDomain;

internal class InMemorySecurityDomain<TEntity>(IList<TEntity> entities)
    : IEntityDomainBase<TEntity>
    where TEntity : AgentSprint.Model.Modules.Common.EntityBase, new()
{
    protected IList<TEntity> Entities { get; } = entities;

    public Task<string> CreateAsync(TEntity entity)
    {
        Entities.Add(entity);
        return Task.FromResult(entity.Id);
    }

    public Task<TEntity?> GetAsync(string id)
    {
        return Task.FromResult(Entities.SingleOrDefault(entity => entity.Id == id && entity.IsDelete == 0));
    }

    public Task<IList<TEntity>> ListAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>>? predicate = null)
    {
        IQueryable<TEntity> query = Entities.AsQueryable().Where(entity => entity.IsDelete == 0);
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return Task.FromResult<IList<TEntity>>(query.ToList());
    }

    public Task<IList<TEntity>> ListIncludingDeletedAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>>? predicate = null)
    {
        IQueryable<TEntity> query = Entities.AsQueryable();
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return Task.FromResult<IList<TEntity>>(query.ToList());
    }

    public Task<string> UpdateAsync(TEntity entity)
    {
        entity.UpdateTime = DateTime.UtcNow;
        return Task.FromResult(entity.Id);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await GetAsync(id);
        if (entity is null)
        {
            return true;
        }

        entity.IsDelete = 1;
        entity.UpdateTime = DateTime.UtcNow;
        return true;
    }
}
