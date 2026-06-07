using AgentSprint.Entry;
using AgentSprint.Service.Services.AgileServices;
using AgentSprint.Service.Services.AuthServices;
using AgentSprint.Service.Services.TestServices;
using AgentSprint.Service.Services.UserServices;

using Air.Cloud.EntityFrameWork.Core.Filters;

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
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAgileMvpService));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITestService));
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
