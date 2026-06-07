using System.Security.Claims;

using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services.UserServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Authorize]
[Route("user")]
public sealed class UserController : ControllerBase
{
    private readonly IUserService _userService;

    /// <summary>
    /// zh-cn: 创建用户控制器，注入用户服务以读取当前登录用户资料。
    /// en-us: Creates the user controller with the user service used to read the current signed-in user's profile.
    /// </summary>
    /// <param name="userService">
    /// zh-cn: 用户服务，用于组装当前用户信息。
    /// en-us: User service used to compose current-user information.
    /// </param>
    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("info")]
    public async Task<ActionResult<ApiResponse<CurrentUserResult>>> GetCurrentUser()
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse<CurrentUserResult>.Error("Authentication is required.", 401));
        }

        return ApiResponse<CurrentUserResult>.Ok(await _userService.GetCurrentUserAsync(userId));
    }

    [HttpGet("options")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserOptionResult>>>> ListUserOptions()
    {
        if (GetUserId() is null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<UserOptionResult>>.Error(
                "Authentication is required.",
                401));
        }

        return ApiResponse<IReadOnlyList<UserOptionResult>>.Ok(await _userService.ListUserOptionsAsync());
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
