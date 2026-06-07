using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentSprint.Mcp;

public sealed class AgentSprintMcpServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly AgentSprintApiClient _client;
    private readonly AgentSprintMcpOptions _options;
    private readonly Dictionary<string, Func<JsonObject, CancellationToken, Task<JsonNode?>>> _tools;

    public AgentSprintMcpServer(AgentSprintApiClient client, AgentSprintMcpOptions options)
    {
        _client = client;
        _options = options;
        _tools = new Dictionary<string, Func<JsonObject, CancellationToken, Task<JsonNode?>>>(StringComparer.Ordinal)
        {
            ["register_session"] = RegisterSessionAsync,
            ["get_agent_skill_pack"] = GetAgentSkillPackAsync,
            ["get_project_bootstrap"] = GetProjectBootstrapAsync,
            ["list_task_hall"] = ListTaskHallAsync,
            ["list_my_tasks"] = ListMyTasksAsync,
            ["get_task_prompt"] = GetTaskPromptAsync,
            ["complete_my_task"] = CompleteMyTaskAsync,
            ["assign_task"] = AssignTaskAsync,
            ["list_test_plans"] = ListTestPlansAsync,
            ["list_bugs"] = ListBugsAsync,
            ["append_agent_event"] = AppendAgentEventAsync,
            ["heartbeat"] = HeartbeatAsync,
            ["close_session"] = CloseSessionAsync
        };
    }

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            line = line.TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var response = await HandleLineAsync(line, cancellationToken);
            if (response is not null)
            {
                await output.WriteLineAsync(response.ToJsonString(JsonOptions));
                await output.FlushAsync();
            }
        }
    }

    /// <summary>
    /// zh-cn: 处理一行 JSON-RPC 请求文本，并返回 MCP 协议响应对象；当收到 initialized 通知等无需响应的消息时返回 null。
    /// en-us: Handles one JSON-RPC request line and returns the MCP protocol response object; returns null for notifications such as initialized that do not require a response.
    /// </summary>
    public async Task<JsonObject?> HandleLineAsync(string line, CancellationToken cancellationToken)
    {
        line = line.TrimStart('\uFEFF');
        JsonObject? request;
        try
        {
            request = JsonNode.Parse(line)?.AsObject();
        }
        catch (JsonException ex)
        {
            return CreateError(null, -32700, $"Parse error: {ex.Message}");
        }

        return await HandleJsonAsync(request, cancellationToken);
    }

    /// <summary>
    /// zh-cn: 处理远程 Streamable HTTP MCP 请求体中的 JSON-RPC 对象，返回可直接序列化到 HTTP 响应的 JSON-RPC 结果；通知类消息返回 null。
    /// en-us: Handles a JSON-RPC object received through the remote Streamable HTTP MCP endpoint and returns a JSON-RPC result ready for HTTP serialization; notifications return null.
    /// </summary>
    /// <param name="request">
    /// zh-cn: MCP JSON-RPC 请求对象。
    /// en-us: MCP JSON-RPC request object.
    /// </param>
    /// <param name="cancellationToken">
    /// zh-cn: 请求取消令牌。
    /// en-us: Request cancellation token.
    /// </param>
    /// <returns>
    /// zh-cn: JSON-RPC 响应对象；无需响应的通知返回 null。
    /// en-us: JSON-RPC response object; notifications that do not require a response return null.
    /// </returns>
    public async Task<JsonObject?> HandleJsonAsync(JsonNode? request, CancellationToken cancellationToken)
    {
        if (request is not JsonObject requestObject)
        {
            return CreateError(null, -32600, "Invalid request.");
        }

        var id = requestObject["id"]?.DeepClone();
        var method = requestObject["method"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(method))
        {
            return CreateError(id, -32600, "Request method is required.");
        }

        try
        {
            return method switch
            {
                "initialize" => CreateResult(id, CreateInitializeResult()),
                "notifications/initialized" => null,
                "tools/list" => CreateResult(id, CreateToolsList()),
                "tools/call" => CreateResult(id, await CallToolAsync(GetParams(requestObject), cancellationToken)),
                _ => CreateError(id, -32601, $"Method '{method}' is not supported.")
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return CreateError(id, -32000, ex.Message);
        }
    }

    private async Task<JsonObject> CallToolAsync(JsonObject parameters, CancellationToken cancellationToken)
    {
        var toolName = parameters["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new ArgumentException("Tool name is required.");
        }

        if (!_tools.TryGetValue(toolName, out var handler))
        {
            throw new ArgumentException($"Tool '{toolName}' is not registered.");
        }

        var arguments = parameters["arguments"]?.AsObject() ?? [];
        var data = await handler(arguments, cancellationToken);
        return new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = data?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}"
                }
            },
            ["structuredContent"] = data?.DeepClone()
        };
    }

    private async Task<JsonNode?> RegisterSessionAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await _client.RegisterSessionAsync(
            GetString(arguments, "project_code"),
            GetString(arguments, "device_code"),
            GetString(arguments, "workspace_path"),
            GetString(arguments, "username"),
            GetString(arguments, "password"),
            cancellationToken);
    }

    private Task<JsonNode?> GetAgentSkillPackAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var projectCode = GetString(arguments, "project_code");
        JsonNode? result = new JsonObject
        {
            ["project_code"] = projectCode,
            ["workspace_path"] = _options.DefaultWorkspacePath,
            ["required_skills"] = new JsonArray
            {
                "air-cloud-quality-gate",
                "browser:control-in-app-browser"
            },
            ["backend_rules"] = new JsonArray
            {
                "All backend code must follow Skill Air.Cloud.xxx quality requirements.",
                "Keep Model, Domain, Repository, Service, Entry layering.",
                "Run dotnet tests for API changes.",
                "Do not expose secrets in task prompts, logs, or MCP responses."
            },
            ["frontend_rules"] = new JsonArray
            {
                "Use list plus detail drawer/dialog patterns for management pages.",
                "Use drawers or dialogs for save forms.",
                "Verify local UI through Browser after meaningful frontend changes."
            },
            ["verification_commands"] = new JsonArray
            {
                "dotnet test F:\\AI\\AgentSprint\\src\\api\\AgentSprint.slnx --no-restore",
                "corepack pnpm -F @vben/web-tdesign run typecheck"
            }
        };

        return Task.FromResult<JsonNode?>(result);
    }

    private async Task<JsonNode?> GetProjectBootstrapAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var projectCode = GetString(arguments, "project_code");
        var projects = await _client.ListProjectsAsync(cancellationToken);
        var requirements = await _client.ListRequirementsAsync(null, cancellationToken);
        var tasks = await _client.ListTaskHallAsync(null, null, null, null, true, cancellationToken);
        var skillPack = await GetAgentSkillPackAsync(arguments, cancellationToken);

        var selectedProject = FindProjectByCode(projects, projectCode);
        var projectId = selectedProject?["id"]?.GetValue<string>();
        return new JsonObject
        {
            ["project"] = selectedProject?.DeepClone(),
            ["project_code"] = projectCode,
            ["project_id"] = projectId,
            ["workspace_path"] = _options.DefaultWorkspacePath,
            ["api_base_url"] = _options.ApiBaseUrl.ToString(),
            ["current_user"] = await _client.GetCurrentUserAsync(cancellationToken),
            ["skill_pack"] = skillPack?.DeepClone(),
            ["requirements"] = FilterByProject(requirements, projectId),
            ["tasks"] = FilterByProject(tasks, projectId)
        };
    }

    private async Task<JsonNode?> ListTaskHallAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await _client.ListTaskHallAsync(
            GetString(arguments, "project_id"),
            GetString(arguments, "requirement_id"),
            GetString(arguments, "assignee_id"),
            GetString(arguments, "status"),
            GetBoolean(arguments, "primary_only", true),
            cancellationToken);
    }

    private async Task<JsonNode?> ListMyTasksAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await _client.ListMyTasksAsync(cancellationToken);
    }

    private async Task<JsonNode?> GetTaskPromptAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var taskId = RequireString(arguments, "task_id");
        var prompt = await _client.GetTaskPromptAsync(taskId, cancellationToken);
        var skillPack = await GetAgentSkillPackAsync(arguments, cancellationToken);
        return new JsonObject
        {
            ["task"] = prompt?.DeepClone(),
            ["skill_pack"] = skillPack?.DeepClone(),
            ["workspace_path"] = _options.DefaultWorkspacePath,
            ["codex_instruction"] = "Use the task prompt together with the skill_pack. Work in the configured workspace, run required verification commands, and call complete_my_task only after the task is genuinely complete."
        };
    }

    private async Task<JsonNode?> CompleteMyTaskAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await _client.CompleteTaskAsync(RequireString(arguments, "task_id"), cancellationToken);
    }

    private async Task<JsonNode?> AssignTaskAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await _client.AssignTaskAsync(
            RequireString(arguments, "task_id"),
            RequireString(arguments, "assignee_id"),
            cancellationToken);
    }

    private async Task<JsonNode?> ListTestPlansAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await _client.ListTestPlansAsync(
            GetString(arguments, "project_id"),
            GetString(arguments, "requirement_id"),
            cancellationToken);
    }

    private async Task<JsonNode?> ListBugsAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await _client.ListBugsAsync(
            GetString(arguments, "project_id"),
            GetString(arguments, "requirement_id"),
            cancellationToken);
    }

    private Task<JsonNode?> AppendAgentEventAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        JsonNode? result = new JsonObject
        {
            ["accepted"] = true,
            ["event_type"] = GetString(arguments, "event_type") ?? "agent_event",
            ["payload"] = arguments["payload"]?.DeepClone(),
            ["recorded_at"] = DateTimeOffset.UtcNow
        };
        return Task.FromResult<JsonNode?>(result);
    }

    private Task<JsonNode?> HeartbeatAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        JsonNode? result = new JsonObject
        {
            ["accepted"] = true,
            ["session_id"] = GetString(arguments, "session_id"),
            ["status"] = GetString(arguments, "status") ?? "online",
            ["current_task"] = GetString(arguments, "current_task"),
            ["recorded_at"] = DateTimeOffset.UtcNow
        };
        return Task.FromResult<JsonNode?>(result);
    }

    private Task<JsonNode?> CloseSessionAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        JsonNode? result = new JsonObject
        {
            ["accepted"] = true,
            ["session_id"] = GetString(arguments, "session_id"),
            ["closed_at"] = DateTimeOffset.UtcNow
        };
        return Task.FromResult<JsonNode?>(result);
    }

    private static JsonObject CreateInitializeResult()
    {
        return new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "agentsprint-mcp",
                ["version"] = "0.1.0"
            },
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject()
            }
        };
    }

    private static JsonObject CreateToolsList()
    {
        var tools = new JsonArray();
        AddTool(tools, "register_session", "Register a local Codex session with AgentSprint and authenticate the MCP bridge.", new JsonObject
        {
            ["project_code"] = StringSchema("Optional project code."),
            ["device_code"] = StringSchema("Optional local device code."),
            ["workspace_path"] = StringSchema("Optional local workspace path."),
            ["username"] = StringSchema("Optional AgentSprint username override."),
            ["password"] = StringSchema("Optional AgentSprint password override.")
        });
        AddTool(tools, "get_agent_skill_pack", "Return AgentSprint Skill and verification instructions for local Codex.", new JsonObject
        {
            ["project_code"] = StringSchema("Optional project code.")
        });
        AddTool(tools, "get_project_bootstrap", "Return project, requirements, tasks, current user, workspace, and Skill pack.", new JsonObject
        {
            ["project_code"] = StringSchema("Project code to bootstrap.")
        });
        AddTool(tools, "list_task_hall", "List task hall items visible to the authenticated AgentSprint user.", TaskFilterSchema());
        AddTool(tools, "list_my_tasks", "List tasks assigned to the authenticated AgentSprint user.", new JsonObject());
        AddTool(tools, "get_task_prompt", "Return the task prompt plus AgentSprint Skill pack for local Codex execution.", new JsonObject
        {
            ["task_id"] = StringSchema("Task identifier.")
        }, ["task_id"]);
        AddTool(tools, "complete_my_task", "Mark an assigned task complete after local Codex work is verified.", new JsonObject
        {
            ["task_id"] = StringSchema("Task identifier.")
        }, ["task_id"]);
        AddTool(tools, "assign_task", "Assign a task to a developer through AgentSprint.", new JsonObject
        {
            ["task_id"] = StringSchema("Task identifier."),
            ["assignee_id"] = StringSchema("Developer user id.")
        }, ["task_id", "assignee_id"]);
        AddTool(tools, "list_test_plans", "List test plans by project or requirement.", ProjectRequirementFilterSchema());
        AddTool(tools, "list_bugs", "List bugs by project or requirement.", ProjectRequirementFilterSchema());
        AddTool(tools, "append_agent_event", "Append a local agent event to the MCP bridge audit stream.", new JsonObject
        {
            ["event_type"] = StringSchema("Event type."),
            ["payload"] = new JsonObject { ["type"] = "object" }
        });
        AddTool(tools, "heartbeat", "Send a local Codex heartbeat.", new JsonObject
        {
            ["session_id"] = StringSchema("Session id."),
            ["status"] = StringSchema("Session status."),
            ["current_task"] = StringSchema("Current task id.")
        });
        AddTool(tools, "close_session", "Close a local Codex MCP session.", new JsonObject
        {
            ["session_id"] = StringSchema("Session id.")
        });

        return new JsonObject { ["tools"] = tools };
    }

    private static void AddTool(
        JsonArray tools,
        string name,
        string description,
        JsonObject properties,
        string[]? required = null)
    {
        tools.Add(new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required is null ? new JsonArray() : new JsonArray(required.Select(value => JsonValue.Create(value)).ToArray())
            }
        });
    }

    private static JsonObject TaskFilterSchema()
    {
        var schema = ProjectRequirementFilterSchema();
        schema["assignee_id"] = StringSchema("Optional assignee user id.");
        schema["status"] = StringSchema("Optional task status.");
        schema["primary_only"] = new JsonObject
        {
            ["type"] = "boolean",
            ["description"] = "Default true. Return tasks where the current Codex user is a primary module, endpoint, or project developer."
        };
        return schema;
    }

    private static JsonObject ProjectRequirementFilterSchema()
    {
        return new JsonObject
        {
            ["project_id"] = StringSchema("Optional project id."),
            ["requirement_id"] = StringSchema("Optional requirement id.")
        };
    }

    private static JsonObject StringSchema(string description)
    {
        return new JsonObject
        {
            ["type"] = "string",
            ["description"] = description
        };
    }

    private static JsonObject GetParams(JsonObject request)
    {
        return request["params"]?.AsObject() ?? [];
    }

    private static JsonObject CreateResult(JsonNode? id, JsonNode result)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = result.DeepClone()
        };
    }

    private static JsonObject CreateError(JsonNode? id, int code, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
    }

    private static string? GetString(JsonObject arguments, string name)
    {
        return arguments.TryGetPropertyValue(name, out var value) ? value?.GetValue<string>() : null;
    }

    private static bool GetBoolean(JsonObject arguments, string name, bool defaultValue)
    {
        return arguments.TryGetPropertyValue(name, out var value) && value is not null
            ? value.GetValue<bool>()
            : defaultValue;
    }

    private static string RequireString(JsonObject arguments, string name)
    {
        var value = GetString(arguments, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.");
        }

        return value;
    }

    private static JsonNode? FindProjectByCode(JsonNode? projects, string? projectCode)
    {
        if (string.IsNullOrWhiteSpace(projectCode) || projects is not JsonArray projectArray)
        {
            return null;
        }

        return projectArray
            .OfType<JsonObject>()
            .FirstOrDefault(project =>
                string.Equals(project["code"]?.GetValue<string>(), projectCode, StringComparison.OrdinalIgnoreCase))
            ?.DeepClone();
    }

    private static JsonArray FilterByProject(JsonNode? items, string? projectId)
    {
        var result = new JsonArray();
        if (items is not JsonArray itemArray)
        {
            return result;
        }

        foreach (var item in itemArray.OfType<JsonObject>())
        {
            if (string.IsNullOrWhiteSpace(projectId) ||
                string.Equals(item["projectId"]?.GetValue<string>(), projectId, StringComparison.Ordinal))
            {
                result.Add(item.DeepClone());
            }
        }

        return result;
    }
}
