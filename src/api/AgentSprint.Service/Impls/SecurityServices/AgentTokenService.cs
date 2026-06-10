using System.Security.Cryptography;
using System.Text;

using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.AuthServices;
using AgentSprint.Service.Services.SecurityServices;

namespace AgentSprint.Service.Impls.SecurityServices;

public sealed class AgentTokenService : AgentSprintServiceBase, IAgentTokenService
{
    private const int TokenLength = 64;
    private readonly IAgentTokenDomain _agentTokenDomain;
    private readonly IAuthService _authService;
    private readonly ISecurityAuthorizationService _authorizationService;
    private readonly IUserDomain _userDomain;

    /// <summary>
    /// zh-cn: 创建 Agent 令牌服务，集中处理管理端令牌维护、令牌哈希校验和 MCP 访问身份换发。
    /// en-us: Creates the Agent token service that centralizes admin token maintenance, token-hash validation, and MCP access identity exchange.
    /// </summary>
    /// <param name="agentTokenDomain">
    /// zh-cn: Agent 令牌领域对象，用于读写令牌元数据。
    /// en-us: Agent token domain used to read and write token metadata.
    /// </param>
    /// <param name="userDomain">
    /// zh-cn: 用户领域对象，用于解析令牌归属人和创建人信息。
    /// en-us: User domain used to resolve token owner and creator information.
    /// </param>
    /// <param name="authorizationService">
    /// zh-cn: 授权服务，用于解析用户角色并判断超级管理员可见范围。
    /// en-us: Authorization service used to resolve user roles and super-administrator visibility.
    /// </param>
    /// <param name="authService">
    /// zh-cn: 认证服务，用于在 MCP 令牌校验通过后为归属用户签发业务 JWT。
    /// en-us: Authentication service used to issue a business JWT for the token owner after MCP token validation succeeds.
    /// </param>
    public AgentTokenService(
        IAgentTokenDomain agentTokenDomain,
        IUserDomain userDomain,
        ISecurityAuthorizationService authorizationService,
        IAuthService authService)
    {
        _agentTokenDomain = agentTokenDomain;
        _userDomain = userDomain;
        _authorizationService = authorizationService;
        _authService = authService;
    }

    public async Task<IReadOnlyList<AgentTokenManagementResult>> ListTokensAsync(
        string currentUserId,
        IReadOnlyList<string> currentRoles,
        string? keyword = null,
        int? status = null)
    {
        var normalizedKeyword = NormalizeOptional(keyword);
        var isSuper = IsSuper(currentRoles);
        var tokens = await _agentTokenDomain.ListAsync(entity =>
            (isSuper || entity.OwnerUserId == currentUserId) &&
            (!status.HasValue || entity.Status == status.Value));
        var users = await _userDomain.ListAsync();
        var userMap = users.ToDictionary(entity => entity.Id, StringComparer.Ordinal);
        return tokens
            .OrderByDescending(entity => entity.CreateTime)
            .Select(entity => MapToken(entity, userMap))
            .Where(entity =>
                string.IsNullOrWhiteSpace(normalizedKeyword) ||
                TextContains(
                    normalizedKeyword,
                    entity.Name,
                    entity.MaskedToken,
                    entity.OwnerUsername,
                    entity.OwnerDisplayName,
                    entity.ProjectId))
            .ToList();
    }

    public async Task<CreatedAgentTokenResult> CreateTokenAsync(CreateAgentTokenRequest request, string currentUserId, IReadOnlyList<string> currentRoles)
    {
        ValidateRequired(request.Name, "Token name is required.");
        if (request.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Token expiration must be later than now.");
        }

        var ownerUserId = string.IsNullOrWhiteSpace(request.OwnerUserId) ? currentUserId : request.OwnerUserId.Trim();
        if (!IsSuper(currentRoles) && ownerUserId != currentUserId)
        {
            throw new InvalidOperationException("Only super administrators can create tokens for other users.");
        }

        var owner = await _userDomain.GetAsync(ownerUserId);
        if (owner is null)
        {
            throw new InvalidOperationException("Token owner does not exist.");
        }

        var rawToken = GenerateToken();
        var entity = new AgentTokenEntity
        {
            Name = request.Name.Trim(),
            TokenHash = HashToken(rawToken),
            TokenValue = rawToken,
            TokenPrefix = rawToken[..8],
            TokenSuffix = rawToken[^8..],
            OwnerUserId = ownerUserId,
            CreatedBy = currentUserId,
            ProjectId = NormalizeOptional(request.ProjectId),
            ExpiresAt = request.ExpiresAt,
            Status = 1
        };
        await _agentTokenDomain.CreateAsync(entity);

        var users = await _userDomain.ListAsync();
        var userMap = users.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return new CreatedAgentTokenResult(entity.Id, rawToken, MapToken(entity, userMap));
    }

    public async Task<bool> RevokeTokenAsync(string id, string currentUserId, IReadOnlyList<string> currentRoles)
    {
        var entity = await _agentTokenDomain.GetAsync(id);
        if (entity is null)
        {
            return true;
        }

        if (!IsSuper(currentRoles) && entity.OwnerUserId != currentUserId)
        {
            throw new InvalidOperationException("You can only revoke your own tokens.");
        }

        entity.Status = 0;
        entity.RevokedAt = DateTime.UtcNow;
        entity.RevokedBy = currentUserId;
        await _agentTokenDomain.UpdateAsync(entity);
        return true;
    }

    public async Task<AgentTokenValidationResult> ValidateForMcpAsync(AgentTokenValidationRequest request)
    {
        ValidateRequired(request.Token, "Agent token is required.");
        var token = request.Token.Trim();
        if (token.Length != TokenLength)
        {
            throw new UnauthorizedAccessException("Agent token is invalid.");
        }

        var entity = await _agentTokenDomain.FindByTokenHashAsync(HashToken(token));
        if (entity is null || entity.Status != 1 || entity.RevokedAt is not null || entity.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Agent token is invalid or expired.");
        }

        var user = await _userDomain.GetAsync(entity.OwnerUserId);
        if (user is null || user.Status != 1)
        {
            throw new UnauthorizedAccessException("Agent token owner is unavailable.");
        }

        entity.LastUsedAt = DateTime.UtcNow;
        await _agentTokenDomain.UpdateAsync(entity);

        var roles = await _authorizationService.ResolveRoleCodesAsync(user.Id);
        var accessToken = _authService.CreateAccessToken(user.Id, user.Username, roles);
        return new AgentTokenValidationResult(
            accessToken,
            user.Id,
            user.Username,
            user.DisplayName,
            entity.ProjectId,
            roles);
    }

    private static AgentTokenManagementResult MapToken(AgentTokenEntity entity, IReadOnlyDictionary<string, UserEntity> users)
    {
        users.TryGetValue(entity.OwnerUserId, out var owner);
        users.TryGetValue(entity.CreatedBy, out var creator);
        return new AgentTokenManagementResult(
            entity.Id,
            entity.Name,
            $"{entity.TokenPrefix}...{entity.TokenSuffix}",
            entity.TokenValue,
            entity.OwnerUserId,
            owner?.Username ?? entity.OwnerUserId,
            owner?.DisplayName ?? entity.OwnerUserId,
            entity.CreatedBy,
            creator?.Username ?? entity.CreatedBy,
            entity.ProjectId,
            entity.ExpiresAt,
            entity.CreateTime,
            entity.LastUsedAt,
            entity.RevokedAt,
            entity.RevokedBy,
            entity.Status);
    }

    private static bool IsSuper(IReadOnlyList<string> roles)
    {
        return roles.Any(role => string.Equals(role, "super", StringComparison.OrdinalIgnoreCase));
    }

    private static string GenerateToken()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = RandomNumberGenerator.GetBytes(TokenLength);
        var builder = new StringBuilder(TokenLength);
        foreach (var value in bytes)
        {
            builder.Append(alphabet[value % alphabet.Length]);
        }

        return builder.ToString();
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool TextContains(string keyword, params string?[] values)
    {
        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }
    }
}
