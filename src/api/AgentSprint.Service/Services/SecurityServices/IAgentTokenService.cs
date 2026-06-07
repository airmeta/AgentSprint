using AgentSprint.Model.Modules.Security.Dtos;

namespace AgentSprint.Service.Services.SecurityServices;

public interface IAgentTokenService
{
    /// <summary>
    /// zh-cn: 查询当前用户可见的 Agent 令牌。普通用户只能看到自己的令牌，超级管理员可以看到所有用户的令牌。
    /// en-us: Lists Agent tokens visible to the current user. Normal users can only see their own tokens, while super administrators can see every user's tokens.
    /// </summary>
    /// <param name="currentUserId">
    /// zh-cn: 当前登录用户编号。
    /// en-us: Current signed-in user id.
    /// </param>
    /// <param name="currentRoles">
    /// zh-cn: 当前用户角色编码集合。
    /// en-us: Current user role-code collection.
    /// </param>
    /// <returns>
    /// zh-cn: 可见令牌元数据集合，不包含完整令牌明文。
    /// en-us: Visible token metadata without full token plaintext.
    /// </returns>
    Task<IReadOnlyList<AgentTokenManagementResult>> ListTokensAsync(string currentUserId, IReadOnlyList<string> currentRoles);

    /// <summary>
    /// zh-cn: 为当前用户或超级管理员指定的用户创建 Agent 令牌，完整令牌明文只在本次返回中出现一次。
    /// en-us: Creates an Agent token for the current user or, for super administrators, a selected user; the full token plaintext is returned only once.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 创建令牌请求，包含名称、到期时间、可选项目和可选归属用户。
    /// en-us: Token creation request containing name, expiration, optional project, and optional owner.
    /// </param>
    /// <param name="currentUserId">
    /// zh-cn: 当前登录用户编号。
    /// en-us: Current signed-in user id.
    /// </param>
    /// <param name="currentRoles">
    /// zh-cn: 当前用户角色编码集合。
    /// en-us: Current user role-code collection.
    /// </param>
    /// <returns>
    /// zh-cn: 新令牌完整明文和可展示的令牌元数据。
    /// en-us: Newly generated full token plaintext and displayable token metadata.
    /// </returns>
    Task<CreatedAgentTokenResult> CreateTokenAsync(CreateAgentTokenRequest request, string currentUserId, IReadOnlyList<string> currentRoles);

    /// <summary>
    /// zh-cn: 撤销指定 Agent 令牌。普通用户只能撤销自己的令牌，超级管理员可以撤销所有令牌。
    /// en-us: Revokes the specified Agent token. Normal users can revoke only their own tokens, while super administrators can revoke any token.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 令牌记录编号。
    /// en-us: Token record id.
    /// </param>
    /// <param name="currentUserId">
    /// zh-cn: 当前登录用户编号。
    /// en-us: Current signed-in user id.
    /// </param>
    /// <param name="currentRoles">
    /// zh-cn: 当前用户角色编码集合。
    /// en-us: Current user role-code collection.
    /// </param>
    /// <returns>
    /// zh-cn: 撤销成功返回 true。
    /// en-us: Returns true when revocation succeeds.
    /// </returns>
    Task<bool> RevokeTokenAsync(string id, string currentUserId, IReadOnlyList<string> currentRoles);

    /// <summary>
    /// zh-cn: 校验远程 MCP 提交的 Agent 令牌，成功后签发代表令牌归属用户的短期业务访问 JWT。
    /// en-us: Validates an Agent token submitted by remote MCP and issues a short-lived business JWT for the token owner.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 包含完整 64 位令牌明文的校验请求。
    /// en-us: Validation request containing the full 64-character token plaintext.
    /// </param>
    /// <returns>
    /// zh-cn: MCP 后续调用主 API 所需的访问令牌和用户上下文。
    /// en-us: Access token and user context required for later MCP calls to the main API.
    /// </returns>
    Task<AgentTokenValidationResult> ValidateForMcpAsync(AgentTokenValidationRequest request);
}
