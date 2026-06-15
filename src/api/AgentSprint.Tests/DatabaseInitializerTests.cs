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
    public async Task StartAsync_WhenAdminExists_PreservesExistingInitializationData()
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
        Assert.Equal("/custom/menus/index", menu.Component);
        Assert.Equal("lucide:custom", menu.Icon);
        Assert.Equal(777, menu.Sort);
        Assert.Equal(1, menu.Type);
        Assert.Equal(0, menu.Status);
        Assert.Equal(1, menu.IsDelete);
        Assert.False(await dbContext.Menus.AnyAsync(entity => entity.Path == "/code-review"));
        Assert.False(await dbContext.Permissions.AnyAsync(entity => entity.Code.StartsWith("Automation:")));
    }

    [Fact]
    public async Task StartAsync_WhenSystemConfigurationExists_DoesNotResetAiPlatform()
    {
        await using var dbContext = CreateDbContext();
        const string customAiPlatform = """{"name":"DevAI","provider":"openai","model":"gpt-5.5","apiKey":"existing-secret","openAiBaseUrl":"https://devai.yunsee.cn","sort":10}""";
        dbContext.Users.Add(new UserEntity
        {
            Username = "admin",
            DisplayName = "Existing Admin",
            PasswordHash = "hash"
        });
        dbContext.Roles.Add(new RoleEntity
        {
            Code = "super",
            Name = "Super Administrator"
        });
        dbContext.SystemConfigurations.Add(new SystemConfigurationEntity
        {
            Key = "AiPlatform:openai",
            Value = customAiPlatform,
            Description = "User configured platform",
            Status = 1
        });
        await dbContext.SaveChangesAsync();

        var initializer = CreateInitializer(dbContext);

        await initializer.StartAsync(CancellationToken.None);

        var aiPlatform = await dbContext.SystemConfigurations.SingleAsync(entity => entity.Key == "AiPlatform:openai");
        Assert.Equal(customAiPlatform, aiPlatform.Value);
        Assert.Equal("User configured platform", aiPlatform.Description);
        Assert.Equal(1, aiPlatform.Status);
    }

    [Fact]
    public async Task StartAsync_WhenTablesAreEmpty_SeedsInitialData()
    {
        await using var dbContext = CreateDbContext();
        var initializer = CreateInitializer(dbContext);

        await initializer.StartAsync(CancellationToken.None);

        Assert.True(await dbContext.Users.AnyAsync(entity => entity.Username == "admin"));
        Assert.True(await dbContext.Menus.AnyAsync(entity => entity.Path == "/automation"));
        Assert.True(await dbContext.SystemConfigurations.AnyAsync(entity => entity.Key == "AiPlatform:openai"));
        Assert.True(await dbContext.DictionaryTypes.AnyAsync(entity => entity.Code == "digital_worker_employee_type"));
        Assert.True(await dbContext.RuntimeEnvironments.AnyAsync(entity => entity.Code == "test" && entity.ProjectId == null));
        Assert.True(await dbContext.PromptTemplates.AnyAsync(entity => entity.Code == "digital_worker_task_execution"));
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
