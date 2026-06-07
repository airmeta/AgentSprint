using System.Security.Claims;

using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services.AuthServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResult>>> Login(LoginRequest request)
    {
        try
        {
            return ApiResponse<LoginResult>.Ok(await _authService.LoginAsync(request));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<LoginResult>.Error(ex.Message, 403));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<LoginResult>.Error(ex.Message, 400));
        }
    }

    [HttpGet("codes")]
    public async Task<ApiResponse<IReadOnlyList<string>>> GetAccessCodes()
    {
        return ApiResponse<IReadOnlyList<string>>.Ok(await _authService.GetAccessCodesAsync(GetUserId()));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public ApiResponse<object> Logout()
    {
        return ApiResponse<object>.Ok(new { });
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    }
}

