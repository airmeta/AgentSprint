using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentSprint.Mcp;

public sealed class AgentSprintApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AgentSprintMcpOptions _options;
    private string? _accessToken;
    private string? _agentToken;
    private string _username;
    private string _password;

    public AgentSprintApiClient(AgentSprintMcpOptions options)
        : this(options, new HttpClient { BaseAddress = options.ApiBaseUrl })
    {
    }

    public AgentSprintApiClient(AgentSprintMcpOptions options, HttpClient httpClient)
    {
        _options = options;
        _httpClient = httpClient;
        _username = options.Username;
        _password = options.Password;
        _accessToken = options.AccessToken;
        _agentToken = options.AgentToken;
    }

    public string CurrentUsername => _username;

    public async Task<JsonNode?> RegisterSessionAsync(
        string? projectCode,
        string? deviceCode,
        string? workspacePath,
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            _username = username.Trim();
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            _password = password;
        }

        if (!string.IsNullOrWhiteSpace(username) ||
            !string.IsNullOrWhiteSpace(password) ||
            (!_options.RequireRequestAuthentication && string.IsNullOrWhiteSpace(_agentToken)))
        {
            _accessToken = null;
        }

        var user = await GetCurrentUserAsync(cancellationToken);
        return new JsonObject
        {
            ["session_id"] = Guid.NewGuid().ToString("N"),
            ["project_code"] = projectCode,
            ["device_code"] = deviceCode,
            ["workspace_path"] = string.IsNullOrWhiteSpace(workspacePath) ? _options.DefaultWorkspacePath : workspacePath,
            ["api_base_url"] = _options.ApiBaseUrl.ToString(),
            ["user"] = user?.DeepClone()
        };
    }

    public async Task<JsonNode?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        return await SendAsync(HttpMethod.Get, "/user/info", null, true, cancellationToken);
    }

    public async Task<JsonNode?> ListProjectsAsync(CancellationToken cancellationToken)
    {
        return await SendAsync(HttpMethod.Get, "/mvp/projects", null, true, cancellationToken);
    }

    public async Task<JsonNode?> ListRequirementsAsync(string? projectId, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(projectId)
            ? "/mvp/requirements"
            : $"/mvp/requirements?projectId={Uri.EscapeDataString(projectId)}";
        return await SendAsync(HttpMethod.Get, path, null, true, cancellationToken);
    }

    public async Task<JsonNode?> ListTaskHallAsync(
        string? projectId,
        string? requirementId,
        string? assigneeId,
        string? status,
        bool primaryOnly,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        AddQuery(query, "projectId", projectId);
        AddQuery(query, "requirementId", requirementId);
        AddQuery(query, "assigneeId", assigneeId);
        AddQuery(query, "status", status);
        if (primaryOnly)
        {
            query.Add("primaryOnly=true");
        }

        var path = query.Count == 0 ? "/mvp/tasks" : $"/mvp/tasks?{string.Join("&", query)}";
        return await SendAsync(HttpMethod.Get, path, null, true, cancellationToken);
    }

    public async Task<JsonNode?> ListMyTasksAsync(CancellationToken cancellationToken)
    {
        return await SendAsync(HttpMethod.Get, "/mvp/tasks/my", null, true, cancellationToken);
    }

    public async Task<JsonNode?> GetTaskPromptAsync(string taskId, CancellationToken cancellationToken)
    {
        return await SendAsync(
            HttpMethod.Get,
            $"/mvp/tasks/{Uri.EscapeDataString(taskId)}/prompt",
            null,
            true,
            cancellationToken);
    }

    public async Task<JsonNode?> CompleteTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        return await SendAsync(
            HttpMethod.Post,
            $"/mvp/tasks/{Uri.EscapeDataString(taskId)}/complete",
            new JsonObject(),
            true,
            cancellationToken);
    }

    public async Task<JsonNode?> AssignTaskAsync(
        string taskId,
        string assigneeId,
        CancellationToken cancellationToken)
    {
        return await SendAsync(
            HttpMethod.Post,
            $"/mvp/tasks/{Uri.EscapeDataString(taskId)}/assign",
            new JsonObject { ["assigneeId"] = assigneeId },
            true,
            cancellationToken);
    }

    public async Task<JsonNode?> ClaimDevelopmentTaskAsync(
        string taskId,
        string? ownerDevice,
        CancellationToken cancellationToken)
    {
        return await SendAsync(
            HttpMethod.Post,
            $"/mvp/tasks/{Uri.EscapeDataString(taskId)}/claim",
            new JsonObject { ["ownerDevice"] = ownerDevice },
            true,
            cancellationToken);
    }

    public async Task<JsonNode?> ListBugsAsync(
        string? projectId,
        string? requirementId,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        AddQuery(query, "projectId", projectId);
        AddQuery(query, "requirementId", requirementId);
        var path = query.Count == 0 ? "/mvp/bugs" : $"/mvp/bugs?{string.Join("&", query)}";
        return await SendAsync(HttpMethod.Get, path, null, true, cancellationToken);
    }

    public async Task<JsonNode?> ListRuntimeEnvironmentsAsync(
        string? projectId,
        string? endpointId,
        string? moduleId,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(
            ("projectId", projectId),
            ("endpointId", endpointId),
            ("moduleId", moduleId));
        return await SendAsync(
            HttpMethod.Get,
            $"/system/runtime-environments{query}",
            null,
            true,
            cancellationToken);
    }

    /// <summary>
    /// zh-cn: 通过 AgentSprint API 查询指定运行环境下的服务容器和端口映射配置，供 MCP 聚合项目测试环境部署信息时使用。
    /// en-us: Lists service container and port-mapping configuration for a runtime environment through the AgentSprint API so MCP tools can aggregate project test deployment details.
    /// </summary>
    /// <param name="runtimeEnvironmentId">
    /// zh-cn: 运行环境标识，不能为空；服务端会按该标识返回未删除的容器映射记录。
    /// en-us: Runtime environment identifier; it must not be empty and the server returns non-deleted container mappings for it.
    /// </param>
    /// <param name="cancellationToken">
    /// zh-cn: 请求取消令牌。
    /// en-us: Request cancellation token.
    /// </param>
    /// <returns>
    /// zh-cn: 平台返回的容器映射列表，字段保持后台接口的 camelCase 命名。
    /// en-us: Container mapping list returned by the platform, preserving the backend API camelCase field names.
    /// </returns>
    public async Task<JsonNode?> ListRuntimeEnvironmentContainersAsync(
        string runtimeEnvironmentId,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(("runtimeEnvironmentId", runtimeEnvironmentId));
        return await SendAsync(
            HttpMethod.Get,
            $"/system/runtime-environment-containers{query}",
            null,
            true,
            cancellationToken);
    }

    /// <summary>
    /// zh-cn: 通过 AgentSprint API 领取指定缺陷并创建缺陷修复租约，设备标识会透传给平台用于恢复本地 Codex 工作状态。
    /// en-us: Claims a specific bug through the AgentSprint API and creates a bug-fix lease; the optional device id is forwarded so the platform can recover local Codex work state.
    /// </summary>
    /// <param name="bugId">
    /// zh-cn: 要领取的缺陷标识。
    /// en-us: Bug identifier to claim.
    /// </param>
    /// <param name="ownerDevice">
    /// zh-cn: 可选的本地设备或会话标识。
    /// en-us: Optional local device or session identifier.
    /// </param>
    /// <param name="cancellationToken">
    /// zh-cn: 请求取消令牌。
    /// en-us: Request cancellation token.
    /// </param>
    /// <returns>
    /// zh-cn: 平台返回的缺陷修复租约。
    /// en-us: Bug-fix lease returned by the platform.
    /// </returns>
    public async Task<JsonNode?> ClaimBugAsync(
        string bugId,
        string? ownerDevice,
        CancellationToken cancellationToken)
    {
        return await SendAsync(
            HttpMethod.Post,
            $"/mvp/bugs/{Uri.EscapeDataString(bugId)}/claim",
            new JsonObject { ["ownerDevice"] = ownerDevice },
            true,
            cancellationToken);
    }

    /// <summary>
    /// zh-cn: 通过 AgentSprint API 将当前缺陷标记为已修复，并由平台关闭对应的活跃缺陷租约。
    /// en-us: Marks the current bug as fixed through the AgentSprint API and lets the platform close the related active bug lease.
    /// </summary>
    /// <param name="bugId">
    /// zh-cn: 要标记已修复的缺陷标识。
    /// en-us: Bug identifier to mark fixed.
    /// </param>
    /// <param name="cancellationToken">
    /// zh-cn: 请求取消令牌。
    /// en-us: Request cancellation token.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的缺陷信息。
    /// en-us: Updated bug information.
    /// </returns>
    public async Task<JsonNode?> FixBugAsync(string bugId, CancellationToken cancellationToken)
    {
        return await SendAsync(
            HttpMethod.Post,
            $"/mvp/bugs/{Uri.EscapeDataString(bugId)}/fix",
            new JsonObject(),
            true,
            cancellationToken);
    }

    public async Task<JsonNode?> ListTestPlansAsync(
        string? projectId,
        string? requirementId,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        AddQuery(query, "projectId", projectId);
        AddQuery(query, "requirementId", requirementId);
        var path = query.Count == 0 ? "/test/plans" : $"/test/plans?{string.Join("&", query)}";
        return await SendAsync(HttpMethod.Get, path, null, true, cancellationToken);
    }

    private async Task<JsonNode?> SendAsync(
        HttpMethod method,
        string path,
        JsonNode? body,
        bool requireAuthentication,
        CancellationToken cancellationToken)
    {
        if (requireAuthentication)
        {
            await EnsureAuthenticatedAsync(cancellationToken);
        }

        using var request = new HttpRequestMessage(method, path);
        if (requireAuthentication)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var envelope = ParseEnvelope(content);
        var code = envelope?["code"]?.GetValue<int>() ?? (response.IsSuccessStatusCode ? 0 : (int)response.StatusCode);
        if (!response.IsSuccessStatusCode || code != 0)
        {
            var message = envelope?["message"]?.GetValue<string>() ?? response.ReasonPhrase ?? "AgentSprint API request failed.";
            throw new InvalidOperationException(message);
        }

        return envelope?["data"]?.DeepClone();
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_agentToken))
        {
            await AuthenticateWithAgentTokenAsync(cancellationToken);
            return;
        }

        if (_options.RequireRequestAuthentication)
        {
            var headerNames = _options.RequestHeaderNames is { Count: > 0 }
                ? string.Join(", ", _options.RequestHeaderNames)
                : "none";
            throw new InvalidOperationException($"Remote MCP request is missing Authorization Bearer, X-AgentSprint-Access-Token, or X-AgentSprint-Agent-Token. Received header names: {headerNames}.");
        }

        await AuthenticateWithPasswordAsync(cancellationToken);
    }

    private async Task AuthenticateWithAgentTokenAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/mcp/auth/token",
            new JsonObject
            {
                ["token"] = _agentToken
            },
            cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var envelope = ParseEnvelope(content);
        var code = envelope?["code"]?.GetValue<int>() ?? (response.IsSuccessStatusCode ? 0 : (int)response.StatusCode);
        if (!response.IsSuccessStatusCode || code != 0)
        {
            var message = envelope?["message"]?.GetValue<string>() ?? response.ReasonPhrase ?? "AgentSprint MCP token validation failed.";
            throw new InvalidOperationException(message);
        }

        _accessToken = envelope?["data"]?["accessToken"]?.GetValue<string>() ??
            throw new InvalidOperationException("AgentSprint MCP token response did not include accessToken.");
        _username = envelope?["data"]?["username"]?.GetValue<string>() ?? _username;
    }

    private async Task AuthenticateWithPasswordAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/auth/login",
            new JsonObject
            {
                ["username"] = _username,
                ["password"] = _password
            },
            cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var envelope = ParseEnvelope(content);
        var code = envelope?["code"]?.GetValue<int>() ?? (response.IsSuccessStatusCode ? 0 : (int)response.StatusCode);
        if (!response.IsSuccessStatusCode || code != 0)
        {
            var message = envelope?["message"]?.GetValue<string>() ?? response.ReasonPhrase ?? "AgentSprint login failed.";
            throw new InvalidOperationException(message);
        }

        _accessToken = envelope?["data"]?["accessToken"]?.GetValue<string>() ??
            throw new InvalidOperationException("AgentSprint login response did not include accessToken.");
    }

    private static JsonNode? ParseEnvelope(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return JsonNode.Parse(content, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
    }

    private static void AddQuery(ICollection<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }
    }

    private static string BuildQuery(params (string Name, string? Value)[] values)
    {
        var query = new List<string>();
        foreach (var (name, value) in values)
        {
            AddQuery(query, name, value);
        }

        return query.Count == 0 ? string.Empty : $"?{string.Join("&", query)}";
    }
}
