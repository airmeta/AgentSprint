using AgentSprint.Entry.Development;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Security;

using Microsoft.Extensions.Options;

namespace AgentSprint.Tests;

public sealed class DevelopmentSecurityStoreTests
{
    [Fact]
    public async Task LoginAsync_AllowsDeveloperAccountAndIssuesDeveloperRole()
    {
        var service = CreateAuthService();

        var result = await service.LoginAsync(new LoginRequest("developer", "123456"));

        Assert.Equal("dev-1", result.Id);
        Assert.Equal("developer", result.Username);
        Assert.Contains("developer", result.Roles);
        Assert.DoesNotContain("super", result.Roles);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [Fact]
    public async Task LoginAsync_RejectsUnknownPassword()
    {
        var service = CreateAuthService();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginRequest("developer", "bad-password")));

        Assert.Equal("Username or password is incorrect.", exception.Message);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsConfiguredDevelopmentRole()
    {
        var service = new DevelopmentUserService();

        var result = await service.GetCurrentUserAsync("pm-1");

        Assert.Equal("pm-1", result.Id);
        Assert.Equal("pm", result.Username);
        Assert.Equal("Product Manager", result.RealName);
        Assert.Contains("pm", result.Roles);
        Assert.DoesNotContain("super", result.Roles);
    }

    [Fact]
    public async Task ListUserOptionsAsync_ExposesAllDevelopmentAccounts()
    {
        var service = new DevelopmentUserService();

        var users = await service.ListUserOptionsAsync();

        Assert.Contains(users, user => user.Id == "dev-admin" && user.Username == "admin");
        Assert.Contains(users, user => user.Id == "pm-1" && user.Username == "pm");
        Assert.Contains(users, user => user.Id == "arch-1" && user.Username == "architect");
        Assert.Contains(users, user => user.Id == "manager-1" && user.Username == "project-manager");
        Assert.Contains(users, user => user.Id == "dev-1" && user.Username == "developer");
        Assert.Contains(users, user => user.Id == "tester-1" && user.Username == "tester");
    }

    private static DevelopmentAuthService CreateAuthService()
    {
        return new DevelopmentAuthService(Options.Create(new JwtOptions
        {
            Audience = "AgentSprint.Admin",
            Issuer = "AgentSprint",
            SigningKey = "AgentSprint-development-signing-key-change-me"
        }));
    }
}
