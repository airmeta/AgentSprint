using AgentSprint.Entry;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Repository.DbContexts;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSprint.Tests;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public async Task StartAsync_WhenAdminExists_EvolvesMenusAndPreservesExistingMenuName()
    {
        await using var dbContext = CreateDbContext();
        var existingMenu = new MenuEntity
        {
            Path = "/system/menus",
            Name = "CustomMenus",
            Component = "/custom/menus/index",
            Icon = "lucide:custom",
            Sort = 777,
            Type = 1,
            Status = 0,
            IsDelete = 1
        };
        dbContext.Users.Add(new UserEntity
        {
            Username = "admin",
            DisplayName = "Existing Admin",
            PasswordHash = "hash"
        });
        var role = new RoleEntity
        {
            Code = "super",
            Name = "Super Administrator"
        };
        dbContext.Roles.Add(role);
        dbContext.Menus.Add(existingMenu);
        await dbContext.SaveChangesAsync();

        var initializer = CreateInitializer(dbContext);

        await initializer.StartAsync(CancellationToken.None);

        var menu = await dbContext.Menus.SingleAsync(entity => entity.Path == "/system/menus");
        Assert.Equal("CustomMenus", menu.Name);
        Assert.Equal("/system/menus/index", menu.Component);
        Assert.Equal("lucide:menu", menu.Icon);
        Assert.Equal(30, menu.Sort);
        Assert.Equal(1, menu.Type);
        Assert.Equal(1, menu.Status);
        Assert.Equal(0, menu.IsDelete);

        var gitGroup = await dbContext.Menus.SingleAsync(entity => entity.Path == "/sprint/git");
        var gitAccounts = await dbContext.Menus.SingleAsync(entity => entity.Path == "/sprint/git/accounts");
        var gitRepositories = await dbContext.Menus.SingleAsync(entity => entity.Path == "/sprint/git/repositories");
        Assert.Equal(gitGroup.Id, gitAccounts.ParentId);
        Assert.Equal(gitGroup.Id, gitRepositories.ParentId);
        Assert.Equal(1, gitAccounts.Status);
        Assert.Equal(1, gitRepositories.Status);

        var gitPermissions = await dbContext.Permissions
            .Where(entity => entity.Code.StartsWith("Sprint:Git"))
            .OrderBy(entity => entity.Code)
            .ToListAsync();
        Assert.Equal(
            [
                "Sprint:GitAccount:Manage",
                "Sprint:GitRepository:BranchCreate",
                "Sprint:GitRepository:BranchDelete",
                "Sprint:GitRepository:Manage",
                "Sprint:GitRepository:PushRecord:Read"
            ],
            gitPermissions.Select(entity => entity.Code));
        Assert.All(gitPermissions, permission => Assert.False(string.IsNullOrWhiteSpace(permission.MenuId)));
        var gitPermissionIds = gitPermissions.Select(permission => permission.Id).ToList();
        Assert.Equal(gitPermissions.Count, await dbContext.RolePermissions.CountAsync(entity => entity.RoleId == role.Id && gitPermissionIds.Contains(entity.PermissionId)));
        Assert.Equal(gitPermissions.Count, await dbContext.EntityAssociations.CountAsync(entity =>
            entity.SourceEntityId == role.Id &&
            entity.AssociationType == SecurityAssociationTypes.RolePermission &&
            gitPermissionIds.Contains(entity.TargetEntityId)));
    }

    private static DefaultDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DefaultDbContext>()
            .UseInMemoryDatabase($"agentsprint-{Guid.NewGuid():N}")
            .Options;

        return new DefaultDbContext(options);
    }

    private static DatabaseInitializer CreateInitializer(DefaultDbContext dbContext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:AutoInitialize"] = "true"
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton(dbContext)
            .BuildServiceProvider();

        return new DatabaseInitializer(configuration, services);
    }
}
