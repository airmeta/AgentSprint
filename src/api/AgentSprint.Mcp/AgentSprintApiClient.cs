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

        _accessToken = null;
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
}
