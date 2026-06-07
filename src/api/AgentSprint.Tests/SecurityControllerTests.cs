using System.Security.Claims;

using AgentSprint.Entry;
using AgentSprint.Entry.Controllers;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services.UserServices;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Tests;

public sealed class SecurityControllerTests
{
    [Fact]
    public async Task GetCurrentUser_ReturnsUnauthorizedEnvelopeWhenIdentityIsMissing()
    {
        var controller = new UserController(new CapturingUserService())
        {
            ControllerContext = CreateContext(null)
        };

        var actionResult = await controller.GetCurrentUser();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<CurrentUserResult>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Authentication is required.", response.Message);
    }

    [Fact]
    public async Task GetAllMenus_ReturnsUnauthorizedEnvelopeWhenIdentityIsMissing()
    {
        var controller = new MenuController(new CapturingUserService())
        {
            ControllerContext = CreateContext(null)
        };

        var actionResult = await controller.GetAll();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<MenuResult>>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Authentication is required.", response.Message);
    }

    [Fact]
    public async Task GetAllMenus_UsesAuthenticatedUserId()
    {
        var service = new CapturingUserService();
        var controller = new MenuController(service)
        {
            ControllerContext = CreateContext("user-1")
        };

        var actionResult = await controller.GetAll();

        var response = Assert.IsType<ApiResponse<IReadOnlyList<MenuResult>>>(actionResult.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("user-1", service.LastUserId);
    }

    [Fact]
    public async Task ListUserOptions_RequiresAuthentication()
    {
        var controller = new UserController(new CapturingUserService())
        {
            ControllerContext = CreateContext(null)
        };

        var actionResult = await controller.ListUserOptions();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<UserOptionResult>>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task ListUserOptions_ReturnsServiceOptions()
    {
        var controller = new UserController(new CapturingUserService())
        {
            ControllerContext = CreateContext("user-1")
        };

        var actionResult = await controller.ListUserOptions();

        var response = Assert.IsType<ApiResponse<IReadOnlyList<UserOptionResult>>>(actionResult.Value);
        Assert.Equal(0, response.Code);
        Assert.Single(response.Data!);
        Assert.Equal("admin", response.Data![0].Username);
    }

    private static ControllerContext CreateContext(string? userId)
    {
        var claims = userId is null
            ? []
            : new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "unit-test"))
            }
        };
    }
}

internal sealed class CapturingUserService : IUserService
{
    public string? LastUserId { get; private set; }

    public Task<CurrentUserResult> GetCurrentUserAsync(string userId)
    {
        LastUserId = userId;
        return Task.FromResult(new CurrentUserResult(userId, userId, "admin", "Administrator", null, ["super"], "/dashboard/workspace", string.Empty, string.Empty));
    }

    public Task<IReadOnlyList<UserOptionResult>> ListUserOptionsAsync()
    {
        IReadOnlyList<UserOptionResult> users = [new("user-1", "admin", "Administrator")];
        return Task.FromResult(users);
    }

    public Task<IReadOnlyList<MenuResult>> GetMenusAsync(string userId)
    {
        LastUserId = userId;
        IReadOnlyList<MenuResult> menus = [];
        return Task.FromResult(menus);
    }
}
