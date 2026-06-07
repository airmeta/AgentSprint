using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services.SecurityServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Route("mcp/auth")]
public sealed class McpAuthController : ControllerBase
{
    private readonly IAgentTokenService _agentTokenService;

    /// <summary>
    /// zh-cn: 创建 MCP 认证控制器，提供远程 MCP 服务用 Agent 令牌换取主 API Bearer 访问令牌的入口。
    /// en-us: Creates the MCP authentication controller that lets the remote MCP service exchange an Agent token for a main API Bearer access token.
    /// </summary>
    /// <param name="agentTokenService">
    /// zh-cn: Agent 令牌服务，负责令牌哈希校验、有效期校验和业务 JWT 签发。
    /// en-us: Agent token service responsible for token-hash validation, expiration checks, and business JWT issuing.
    /// </param>
    public McpAuthController(IAgentTokenService agentTokenService)
    {
        _agentTokenService = agentTokenService;
    }

    /// <summary>
    /// zh-cn: 校验远程 MCP 请求携带的完整 Agent 令牌。接口匿名开放，但只接受有效令牌，成功后返回令牌归属用户的短期访问上下文。
    /// en-us: Validates the full Agent token carried by a remote MCP request. The endpoint is anonymous but accepts only valid tokens and returns a short-lived access context for the token owner.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 包含完整 64 位 Agent 令牌的请求。
    /// en-us: Request containing the full 64-character Agent token.
    /// </param>
    /// <returns>
    /// zh-cn: 可供 MCP 后续调用主 API 使用的访问令牌和用户上下文。
    /// en-us: Access token and user context usable by MCP for subsequent calls to the main API.
    /// </returns>
    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<ActionResult<ApiResponse<AgentTokenValidationResult>>> ValidateToken(AgentTokenValidationRequest request)
    {
        try
        {
            return ApiResponse<AgentTokenValidationResult>.Ok(await _agentTokenService.ValidateForMcpAsync(request));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse<AgentTokenValidationResult>.Error(ex.Message, 401));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AgentTokenValidationResult>.Error(ex.Message, 400));
        }
    }
}
