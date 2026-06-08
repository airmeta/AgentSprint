using AgentSprint.Entry;
using AgentSprint.Service.Services.AuthServices;
using AgentSprint.Service.Services.UserServices;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Service.Impls.AgileServices;
using AgentSprint.Service.Impls.TestServices;

using Air.Cloud.Core.Standard.DataBase.Domains;
using Air.Cloud.Core.Standard.DynamicServer;
using Air.Cloud.EntityFrameWork.Core.Filters;
using Air.Cloud.WebApp.UnifyResult.Providers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentSprint.Tests;

public sealed class StartupConfigurationTests
{
    [Fact]
    public void ConfigureServices_RegistersAirCloudAutoSaveChangesFilter()
    {
        var services = new ServiceCollection();
        var startup = new Startup();

        WithDevelopmentStartupEnvironment(() =>
        {
            startup.ConfigureServices(services);

            using var serviceProvider = services.BuildServiceProvider();
            var mvcOptions = serviceProvider.GetRequiredService<IOptions<MvcOptions>>().Value;

            Assert.Contains(
                mvcOptions.Filters,
                filter => IsAutoSaveChangesFilterRegistration(filter));
        });
    }

    [Fact]
    public void ConfigureServices_UsesDevelopmentSecurityWithoutDroppingBusinessServices()
    {
        var services = new ServiceCollection();
        var startup = new Startup();

        WithDevelopmentStartupEnvironment(() =>
        {
            startup.ConfigureServices(services);

            Assert.Contains(
                services,
                descriptor => descriptor.ServiceType == typeof(IAuthService) &&
                    descriptor.ImplementationType?.Name == "DevelopmentAuthService");
            Assert.Contains(
                services,
                descriptor => descriptor.ServiceType == typeof(IUserService) &&
                    descriptor.ImplementationType?.Name == "DevelopmentUserService");
        });
    }

    [Fact]
    public void BusinessServices_FollowAirCloudDynamicServiceContract()
    {
        Assert.True(typeof(IDynamicService).IsAssignableFrom(typeof(AgileMvpService)));
        Assert.True(typeof(ITransient).IsAssignableFrom(typeof(AgileMvpService)));
        Assert.True(typeof(IDynamicService).IsAssignableFrom(typeof(TestService)));
        Assert.True(typeof(ITransient).IsAssignableFrom(typeof(TestService)));
    }

    [Fact]
    public void DomainInterfaces_FollowAirCloudEntityDomainScanningContract()
    {
        Assert.True(typeof(IEntityDomain).IsAssignableFrom(typeof(IUserDomain)));
        Assert.True(typeof(ITransient).IsAssignableFrom(typeof(IUserDomain)));
        Assert.True(typeof(IEntityDomain).IsAssignableFrom(typeof(IDictionaryTypeDomain)));
        Assert.True(typeof(ITransient).IsAssignableFrom(typeof(IDictionaryTypeDomain)));
        Assert.True(typeof(IEntityDomain).IsAssignableFrom(typeof(IDictionaryItemDomain)));
        Assert.True(typeof(ITransient).IsAssignableFrom(typeof(IDictionaryItemDomain)));
        Assert.True(typeof(IEntityDomain).IsAssignableFrom(typeof(IRuntimeEnvironmentDomain)));
        Assert.True(typeof(ITransient).IsAssignableFrom(typeof(IRuntimeEnvironmentDomain)));
        Assert.True(typeof(IEntityDomain).IsAssignableFrom(typeof(IRuntimeEnvironmentContainerDomain)));
        Assert.True(typeof(ITransient).IsAssignableFrom(typeof(IRuntimeEnvironmentContainerDomain)));
        Assert.True(typeof(IEntityDomain).IsAssignableFrom(typeof(IPromptTemplateDomain)));
        Assert.True(typeof(ITransient).IsAssignableFrom(typeof(IPromptTemplateDomain)));
    }

    [Fact]
    public void ConfigureServices_RegistersAgentSprintUnifyResultProvider()
    {
        var services = new ServiceCollection();
        var startup = new Startup();

        WithDevelopmentStartupEnvironment(() =>
        {
            startup.ConfigureServices(services);

            Assert.Contains(
                services,
                descriptor => descriptor.ServiceType == typeof(IUnifyResultProvider) &&
                    descriptor.ImplementationType == typeof(AgentSprintUnifyResultProvider));
        });
    }

    private static bool IsAutoSaveChangesFilterRegistration(IFilterMetadata filter)
    {
        return filter.GetType().Name == nameof(AutoSaveChangesFilter) ||
            filter is TypeFilterAttribute typeFilter &&
            typeFilter.ImplementationType == typeof(AutoSaveChangesFilter);
    }

    private static void WithDevelopmentStartupEnvironment(Action action)
    {
        var previousUseInMemorySecurity = Environment.GetEnvironmentVariable("Database__UseInMemorySecurity");
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AgentSprintConnectionString");

        try
        {
            Environment.SetEnvironmentVariable("Database__UseInMemorySecurity", "true");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__AgentSprintConnectionString",
                "server=127.0.0.1;port=3306;database=agentsprint_test;user=root;password=example;");

            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("Database__UseInMemorySecurity", previousUseInMemorySecurity);
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__AgentSprintConnectionString",
                previousConnectionString);
        }
    }
}
