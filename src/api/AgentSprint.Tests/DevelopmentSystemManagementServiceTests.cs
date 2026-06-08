using AgentSprint.Entry.Development;

namespace AgentSprint.Tests;

public sealed class DevelopmentSystemManagementServiceTests
{
    [Fact]
    public async Task ListMenusAsync_IncludesRootPersonaSystemAndSecurityMenus()
    {
        var service = new DevelopmentSystemManagementService();

        var menus = await service.ListMenusAsync();

        Assert.DoesNotContain(menus, menu => menu.Path == "/sprint");
        Assert.DoesNotContain(menus, menu => menu.Path == "/dashboard");
        Assert.Contains(menus, menu => menu.Path == "/dashboard/workspace" && menu.ParentId is null);
        Assert.DoesNotContain(menus, menu => menu.Path == "/dashboard/analytics");
        Assert.Contains(menus, menu => menu.Path == "/sprint/project" && menu.ParentId is null);
        Assert.Contains(menus, menu => menu.Path == "/sprint/product" && menu.ParentId is null);
        Assert.Contains(menus, menu => menu.Path == "/sprint/worker" && menu.ParentId is null);
        Assert.Contains(menus, menu => menu.Path == "/sprint/test" && menu.ParentId is null);
        Assert.Contains(menus, menu => menu.Path == "/sprint/projects" && menu.ParentId == "menu-sprint-project");
        Assert.Contains(menus, menu => menu.Path == "/sprint/requirements" && menu.ParentId == "menu-sprint-product");
        Assert.Contains(menus, menu => menu.Path == "/sprint/tasks" && menu.ParentId == "menu-sprint-worker");
        Assert.Contains(menus, menu => menu.Path == "/sprint/tests" && menu.ParentId == "menu-sprint-test");
        Assert.Contains(menus, menu => menu.Path == "/sprint/defects" && menu.ParentId == "menu-sprint-test");
        Assert.Contains(menus, menu => menu.Path == "/system/menus" && menu.ParentId == "menu-system");
        Assert.DoesNotContain(menus, menu => menu.Path == "/system/permissions");
        Assert.Contains(menus, menu => menu.Path == "/system/dictionaries" && menu.ParentId == "menu-system");
        Assert.DoesNotContain(menus, menu => menu.Path == "/system/runtime-environments");
        Assert.DoesNotContain(menus, menu => menu.Path == "/system/prompt-templates");
        Assert.Contains(menus, menu => menu.Path == "/system/configurations" && menu.ParentId == "menu-system");
        Assert.Contains(menus, menu => menu.Path == "/system/departments" && menu.ParentId == "menu-system");
        Assert.Contains(menus, menu => menu.Path == "/system/assignments" && menu.ParentId == "menu-system");
        Assert.Contains(menus, menu => menu.Path == "/global-config" && menu.ParentId is null && menu.Name == "GlobalConfig");
        Assert.Contains(menus, menu => menu.Path == "/global-config/environments" && menu.ParentId == "menu-global-config");
        Assert.Contains(menus, menu => menu.Path == "/global-config/prompt-templates" && menu.ParentId == "menu-global-config");
        Assert.Contains(menus, menu => menu.Path == "/security" && menu.ParentId is null && menu.Icon == "lucide:shield-check");
        Assert.Contains(menus, menu => menu.Path == "/system/agent-tokens" && menu.ParentId == "menu-security");
        Assert.DoesNotContain(menus, menu => menu.Path == "/system/org");
        Assert.DoesNotContain(menus, menu => menu.Path.StartsWith("/demos", StringComparison.Ordinal));
        Assert.DoesNotContain(menus, menu => menu.Path.StartsWith("/vben-admin", StringComparison.Ordinal));
    }
}
