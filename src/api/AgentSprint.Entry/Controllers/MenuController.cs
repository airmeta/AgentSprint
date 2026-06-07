using System.Security.Claims;

using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services.UserServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Authorize]
[Route("menu")]
public sealed class MenuController : ControllerBase
{
    private readonly IUserService _userService;

    /// <summary>
    /// zh-cn: 创建菜单控制器，注入用户服务以按当前用户授权返回后端动态菜单。
    /// en-us: Creates the menu controller with the user service used to return backend-driven menus authorized for the current user.
    /// </summary>
    /// <param name="userService">
    /// zh-cn: 用户服务，用于读取角色授权菜单树。
    /// en-us: User service used to read the role-authorized menu tree.
    /// </param>
    public MenuController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("all")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuResult>>>> GetAll()
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<MenuResult>>.Error("Authentication is required.", 401));
        }

        return ApiResponse<IReadOnlyList<MenuResult>>.Ok(await _userService.GetMenusAsync(userId));
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
