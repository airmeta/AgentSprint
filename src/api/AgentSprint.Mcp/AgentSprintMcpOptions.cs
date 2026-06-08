namespace AgentSprint.Mcp;

public sealed record AgentSprintMcpOptions(
    Uri ApiBaseUrl,
    string Username,
    string Password,
    string? AccessToken,
    string? AgentToken,
    string DefaultWorkspacePath,
    bool RequireRequestAuthentication,
    IReadOnlyList<string>? RequestHeaderNames = null)
{
    private const int AgentTokenLength = 64;

    public static AgentSprintMcpOptions FromEnvironment()
    {
        var apiBaseUrl = Environment.GetEnvironmentVariable("AGENTSPRINT_API_BASE_URL");
        var username = Environment.GetEnvironmentVariable("AGENTSPRINT_MCP_USERNAME");
        var password = Environment.GetEnvironmentVariable("AGENTSPRINT_MCP_PASSWORD");
        var accessToken = Environment.GetEnvironmentVariable("AGENTSPRINT_MCP_ACCESS_TOKEN");
        var agentToken = Environment.GetEnvironmentVariable("AGENTSPRINT_MCP_AGENT_TOKEN");
        var workspacePath = Environment.GetEnvironmentVariable("AGENTSPRINT_WORKSPACE_PATH");

        return new AgentSprintMcpOptions(
            new Uri(string.IsNullOrWhiteSpace(apiBaseUrl) ? "http://localhost:5000" : apiBaseUrl),
            string.IsNullOrWhiteSpace(username) ? "developer" : username,
            string.IsNullOrWhiteSpace(password) ? "123456" : password,
            string.IsNullOrWhiteSpace(accessToken) ? null : accessToken,
            string.IsNullOrWhiteSpace(agentToken) ? null : agentToken,
            string.IsNullOrWhiteSpace(workspacePath) ? "F:\\AI\\AgentSprint" : workspacePath,
            false,
            null);
    }

    /// <summary>
    /// zh-cn: 从远程 HTTP MCP 请求上下文创建 AgentSprint 连接配置；API 地址默认来自环境变量，仅在请求头明确提供远程 MCP 可访问地址时覆盖，用户和令牌也可由请求头覆盖，确保每个远程 Codex 客户端请求拥有独立身份上下文。
    /// en-us: Creates AgentSprint connection options from a remote HTTP MCP request context; the API URL defaults to environment variables and is overridden only when the request header explicitly provides an address reachable by the remote MCP service, while user and token can also be overridden by request headers.
    /// </summary>
    /// <param name="httpContext">
    /// zh-cn: 当前 MCP HTTP 请求上下文。
    /// en-us: Current MCP HTTP request context.
    /// </param>
    /// <returns>
    /// zh-cn: 可用于调用 AgentSprint API 的 MCP 配置。
    /// en-us: MCP options used to call the AgentSprint API.
    /// </returns>
    public static AgentSprintMcpOptions FromHttpContext(HttpContext httpContext)
    {
        var fallback = FromEnvironment();
        var apiBaseUrl = ReadHeader(httpContext, "X-AgentSprint-Api-Base-Url");
        var username = ReadHeader(httpContext, "X-AgentSprint-Username");
        var password = ReadHeader(httpContext, "X-AgentSprint-Password");
        var accessToken = ReadHeader(httpContext, "X-AgentSprint-Access-Token");
        var agentToken = ReadHeader(httpContext, "X-AgentSprint-Agent-Token");
        ApplyBearerToken(ReadBearerToken(httpContext), ref accessToken, ref agentToken);
        var workspacePath = ReadHeader(httpContext, "X-AgentSprint-Workspace-Path");

        return new AgentSprintMcpOptions(
            new Uri(string.IsNullOrWhiteSpace(apiBaseUrl) ? fallback.ApiBaseUrl.ToString() : apiBaseUrl),
            string.IsNullOrWhiteSpace(username) ? fallback.Username : username,
            string.IsNullOrWhiteSpace(password) ? fallback.Password : password,
            string.IsNullOrWhiteSpace(accessToken) ? null : accessToken,
            string.IsNullOrWhiteSpace(agentToken) ? null : agentToken,
            string.IsNullOrWhiteSpace(workspacePath) ? fallback.DefaultWorkspacePath : workspacePath,
            true,
            httpContext.Request.Headers.Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string? ReadHeader(HttpContext httpContext, string name)
    {
        return httpContext.Request.Headers.TryGetValue(name, out var values) ? values.ToString() : null;
    }

    private static string? ReadBearerToken(HttpContext httpContext)
    {
        var authorization = ReadHeader(httpContext, "Authorization");
        const string bearerPrefix = "Bearer ";
        return authorization?.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) == true
            ? authorization[bearerPrefix.Length..].Trim()
            : null;
    }

    private static void ApplyBearerToken(string? bearerToken, ref string? accessToken, ref string? agentToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return;
        }

        if (bearerToken.Contains('.', StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(accessToken))
        {
            accessToken ??= bearerToken;
            return;
        }

        if (bearerToken.Length == AgentTokenLength ||
            !string.IsNullOrWhiteSpace(agentToken))
        {
            agentToken ??= bearerToken;
            return;
        }

        accessToken = bearerToken;
    }

}
