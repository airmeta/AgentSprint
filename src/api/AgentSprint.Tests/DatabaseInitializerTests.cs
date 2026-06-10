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
    public async Task StartAsync_WhenAdminExists_DoesNotSeedOrModifyMenus()
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
        dbContext.Menus.Add(existingMenu);
        await dbContext.SaveChangesAsync();

        var initializer = CreateInitializer(dbContext);

        await initializer.StartAsync(CancellationToken.None);

        var menu = await dbContext.Menus.SingleAsync(entity => entity.Path == "/system/menus");
        Assert.Equal("CustomMenus", menu.Name);
        Assert.Equal("/custom/menus/index", menu.Component);
        Assert.Equal("lucide:custom", menu.Icon);
        Assert.Equal(777, menu.Sort);
        Assert.Equal(1, menu.Type);
        Assert.Equal(0, menu.Status);
        Assert.Equal(1, menu.IsDelete);
        Assert.False(await dbContext.Menus.AnyAsync(entity => entity.Path == "/system"));
        Assert.False(await dbContext.Roles.AnyAsync(entity => entity.Code == "super"));
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
