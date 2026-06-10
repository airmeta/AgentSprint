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
    private const int PollingBaseIntervalSeconds = 30;
    private const int PollingStepSeconds = 30;
    private const int PollingMaxIntervalSeconds = 180;
    private const string ProjectTestDeploymentNotice = "你必须按照提示词和脚本来进行部署系统,如果脚本出现异常,直接提示用户异常原因是什么,不要尝试更换方式去绕过脚本";
    private static readonly Dictionary<string, int> MyTaskStatusPriority = new(StringComparer.Ordinal)
    {
        ["in_progress"] = 0,
        ["assigned"] = 1,
        ["pending_assign"] = 2
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
            ["get_mcp_tool_guide"] = GetMcpToolGuideAsync,
            ["get_agent_skill_pack"] = GetAgentSkillPackAsync,
            ["get_project_bootstrap"] = GetProjectBootstrapAsync,
            ["get_runtime_environment"] = GetRuntimeEnvironmentAsync,
            ["get_project_test_deployment"] = GetProjectTestDeploymentAsync,
            ["list_task_hall"] = ListTaskHallAsync,
            ["list_my_tasks"] = ListMyTasksAsync,
            ["get_task_prompt"] = GetTaskPromptAsync,
            ["complete_my_task"] = CompleteMyTaskAsync,
            ["assign_task"] = AssignTaskAsync,
            ["get_next_work"] = GetNextWorkAsync,
            ["claim_next_work"] = ClaimNextWorkAsync,
            ["list_test_plans"] = ListTestPlansAsync,
            ["list_bugs"] = ListBugsAsync,
            ["list_my_open_bugs"] = ListMyOpenBugsAsync,
            ["claim_bug"] = ClaimBugAsync,
            ["fix_bug"] = FixBugAsync,
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

    private Task<JsonNode?> GetMcpToolGuideAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var format = GetString(arguments, "format") ?? "summary";
        JsonNode? result = new JsonObject
        {
            ["format"] = format,
            ["purpose"] = "Guide Codex to use AgentSprint MCP tools for session setup, context loading, task execution, completion callback, and follow-up work discovery.",
            ["recommended_flow"] = new JsonArray
            {
                "Call register_session before reading project or task context.",
                "Call get_agent_skill_pack to load required skills, backend rules, frontend rules, and verification commands.",
                "Call get_task_prompt with task_id for task-specific work; use get_project_bootstrap when a project-level overview is needed.",
                "Run relevant backend tests and frontend checks before calling complete_my_task.",
                "Read complete_my_task.next_work as status context, then stop by default; do not call get_next_work or claim_next_work unless the user explicitly asks to continue."
            },
            ["next_work_priority"] = new JsonArray
            {
                "Open or currently fixing bugs under requirements completed by the current Codex user in the current project.",
                "Incomplete tasks returned by list_my_tasks for the current Codex user in the current project, prioritized as in_progress, assigned, then pending_assign.",
                "Visible pending task-hall items in the current project that can be assigned to the current Codex user."
            },
            ["delivery_options"] = new JsonArray
            {
                CreateDeliveryOption("prompt", "Use for short startup flow and current-task completion instructions; avoid embedding the full guide in every task prompt."),
                CreateDeliveryOption("http_link", "Use for the full human-readable guide when Codex can access the documentation URL."),
                CreateDeliveryOption("skill", "Use for long-lived AgentSprint workflow rules installed in the Codex client."),
                CreateDeliveryOption("tools_list", "Use as the authoritative live schema after Codex connects to MCP.")
            },
            ["safety_rules"] = new JsonArray
            {
                "Do not include SSH private keys, database passwords, Agent Tokens, or server connection strings in prompts, logs, code, or MCP responses.",
                "Do not call complete_my_task until local verification has passed.",
                "Prefer structuredContent over text content when reading MCP tool results.",
                "Do not call get_next_work or claim_next_work without an explicit user request and project_id; no project context means no cross-project polling or claiming.",
                "After completing the current task, close the session or stop the workflow instead of polling for new work by default."
            },
            ["tools"] = CreateMcpToolGuideTools()
        };

        if (string.Equals(format, "full", StringComparison.OrdinalIgnoreCase))
        {
            result["return_shapes"] = CreateMcpToolGuideReturnShapes();
            result["prompt_snippet"] = "Complete the current task, run local verification, call agentsprint.complete_my_task, inspect next_work as status context, then stop. Do not poll get_next_work or call claim_next_work unless the user explicitly asks to continue with another item.";
        }

        return Task.FromResult<JsonNode?>(result);
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

    private async Task<JsonNode?> GetRuntimeEnvironmentAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var projects = await _client.ListProjectsAsync(cancellationToken);
        var task = await ResolveOptionalTaskAsync(GetString(arguments, "task_id"), cancellationToken);
        var projectId = GetString(arguments, "project_id") ?? GetString(task, "projectId");
        var requirementId = GetString(arguments, "requirement_id") ?? GetString(task, "requirementId");
        var endpointId = GetString(arguments, "endpoint_id") ?? GetString(task, "endpointId");
        var moduleId = GetString(arguments, "module_id") ?? GetString(task, "moduleId");

        if (string.IsNullOrWhiteSpace(projectId))
        {
            var project = FindProjectByCode(projects, GetString(arguments, "project_code"));
            projectId = GetString(project, "id");
        }

        JsonNode? requirement = null;
        if (!string.IsNullOrWhiteSpace(requirementId))
        {
            var requirements = await _client.ListRequirementsAsync(projectId, cancellationToken);
            requirement = FindById(requirements, requirementId);
            projectId ??= GetString(requirement, "projectId");
            endpointId ??= GetString(requirement, "endpointId");
            moduleId ??= GetString(requirement, "moduleId");
        }

        var selectedProject = FindById(projects, projectId);
        var environments = await _client.ListRuntimeEnvironmentsAsync(
            projectId,
            endpointId,
            moduleId,
            cancellationToken);
        var selectedEnvironment = SelectRuntimeEnvironment(
            environments,
            GetString(selectedProject, "testEnvironmentId"));

        return new JsonObject
        {
            ["project"] = selectedProject?.DeepClone(),
            ["task"] = task?.DeepClone(),
            ["requirement"] = requirement?.DeepClone(),
            ["selected_environment"] = selectedEnvironment?.DeepClone(),
            ["environments"] = environments?.DeepClone(),
            ["test_url"] = ResolveRuntimeEnvironmentUrl(selectedEnvironment) ??
                GetString(selectedProject, "testEnvironmentUrl"),
            ["resolution"] = new JsonObject
            {
                ["project_id"] = projectId,
                ["requirement_id"] = requirementId,
                ["task_id"] = GetString(arguments, "task_id"),
                ["endpoint_id"] = endpointId,
                ["module_id"] = moduleId
            }
        };
    }

    private async Task<JsonNode?> GetProjectTestDeploymentAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var projectId = RequireString(arguments, "project_id");
        var projects = await _client.ListProjectsAsync(cancellationToken);
        var selectedProject = FindById(projects, projectId);
        var environments = await _client.ListRuntimeEnvironmentsAsync(
            projectId,
            null,
            null,
            cancellationToken);
        var selectedEnvironment = SelectRuntimeEnvironment(
            environments,
            GetString(selectedProject, "testEnvironmentId"));
        var selectedEnvironmentId = GetString(selectedEnvironment, "id");
        var containers = string.IsNullOrWhiteSpace(selectedEnvironmentId)
            ? new JsonArray()
            : CloneArray(await _client.ListRuntimeEnvironmentContainersAsync(
                selectedEnvironmentId,
                cancellationToken));

        return new JsonObject
        {
            ["project"] = selectedProject?.DeepClone(),
            ["project_id"] = projectId,
            ["selected_environment"] = selectedEnvironment?.DeepClone(),
            ["deployment"] = CreateDeploymentInfo(selectedProject, selectedEnvironment),
            ["containers"] = containers.DeepClone(),
            ["test_url"] = ResolveRuntimeEnvironmentUrl(selectedEnvironment) ??
                GetString(selectedProject, "testEnvironmentUrl"),
            ["notice"] = ProjectTestDeploymentNotice,
            ["resolution"] = new JsonObject
            {
                ["project_id"] = projectId,
                ["environment_id"] = selectedEnvironmentId,
                ["environment_code"] = GetString(selectedEnvironment, "code"),
                ["environment_type"] = GetString(selectedEnvironment, "environmentType"),
                ["container_count"] = containers.Count,
                ["active_container_count"] = CountActiveContainers(containers)
            }
        };
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
        var task = await ResolveTaskByIdAsync(taskId, cancellationToken);
        var projectId = GetString(task, "projectId");
        var requirementId = GetString(task, "requirementId");
        var requirements = await _client.ListRequirementsAsync(projectId, cancellationToken);
        var requirement = FindById(requirements, requirementId);

        return new JsonObject
        {
            ["task_id"] = taskId,
            ["task_prompt"] = prompt?.DeepClone(),
            ["task_detail"] = task?.DeepClone(),
            ["requirement_detail"] = requirement?.DeepClone(),
            ["skill_pack"] = skillPack?.DeepClone(),
            ["workspace_path"] = _options.DefaultWorkspacePath,
            ["codex_instruction"] = "Use task_id to keep completion status unambiguous. Load task_detail and requirement_detail from this MCP response, work in the configured workspace, run required verification commands, and call complete_my_task with the same task_id only after the task is genuinely complete."
        };
    }

    private async Task<JsonNode?> CompleteMyTaskAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var completedTask = await _client.CompleteTaskAsync(RequireString(arguments, "task_id"), cancellationToken);
        var nextWorkArguments = EnsureNextWorkProjectContext(arguments, completedTask);
        var nextWork = await BuildNextWorkAsync(nextWorkArguments, claimWork: false, cancellationToken);
        return new JsonObject
        {
            ["completed_task"] = completedTask?.DeepClone(),
            ["next_work"] = nextWork.DeepClone()
        };
    }

    private async Task<JsonNode?> AssignTaskAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await _client.AssignTaskAsync(
            RequireString(arguments, "task_id"),
            RequireString(arguments, "assignee_id"),
            cancellationToken);
    }

    private async Task<JsonNode?> GetNextWorkAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await BuildNextWorkAsync(arguments, claimWork: false, cancellationToken);
    }

    private async Task<JsonNode?> ClaimNextWorkAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await BuildNextWorkAsync(arguments, claimWork: true, cancellationToken);
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

    private async Task<JsonNode?> ListMyOpenBugsAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var currentUser = await _client.GetCurrentUserAsync(cancellationToken);
        var currentUserId = GetString(currentUser, "id") ?? GetString(currentUser, "userId");
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            throw new InvalidOperationException("Current user id is required to resolve related bugs.");
        }

        var myTasks = await _client.ListMyTasksAsync(cancellationToken);
        var relatedRequirementIds = CollectRequirementIds(
            myTasks,
            currentUserId,
            includeCompletedOnly: true,
            GetString(arguments, "project_id"));
        var requirementId = GetString(arguments, "requirement_id");
        if (!string.IsNullOrWhiteSpace(requirementId))
        {
            relatedRequirementIds.Clear();
            relatedRequirementIds.Add(requirementId);
        }

        var allBugs = await _client.ListBugsAsync(GetString(arguments, "project_id"), null, cancellationToken);
        return FilterBugs(
            allBugs,
            GetString(arguments, "project_id"),
            relatedRequirementIds,
            includeFixingOwnedBy: currentUserId);
    }

    private async Task<JsonNode?> ClaimBugAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await _client.ClaimBugAsync(
            RequireString(arguments, "bug_id"),
            GetString(arguments, "owner_device"),
            cancellationToken);
    }

    private async Task<JsonNode?> FixBugAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        return await _client.FixBugAsync(RequireString(arguments, "bug_id"), cancellationToken);
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
        var status = GetString(arguments, "status") ?? "online";
        JsonNode? result = new JsonObject
        {
            ["accepted"] = true,
            ["session_id"] = GetString(arguments, "session_id"),
            ["status"] = status,
            ["current_task"] = GetString(arguments, "current_task"),
            ["recorded_at"] = DateTimeOffset.UtcNow,
            ["polling"] = CreatePollingState(arguments, shouldContinue: status != "closed"),
            ["session"] = CreateSessionState(arguments, status)
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
        AddTool(tools, "get_mcp_tool_guide", "Return a human-readable AgentSprint MCP tool guide, including usage flow, parameters, return shapes, and Codex delivery options.", new JsonObject
        {
            ["format"] = StringSchema("Optional guide format: summary or full. Default summary.")
        });
        AddTool(tools, "get_agent_skill_pack", "Return AgentSprint Skill and verification instructions for local Codex.", new JsonObject
        {
            ["project_code"] = StringSchema("Optional project code.")
        });
        AddTool(tools, "get_project_bootstrap", "Return project, requirements, tasks, current user, workspace, and Skill pack.", new JsonObject
        {
            ["project_code"] = StringSchema("Project code to bootstrap.")
        });
        AddTool(tools, "get_runtime_environment", "Resolve runtime environment configuration by project, task, or requirement id for Codex and frontend test-environment lookups.", new JsonObject
        {
            ["project_id"] = StringSchema("Optional project id."),
            ["project_code"] = StringSchema("Optional project code."),
            ["task_id"] = StringSchema("Optional development task id."),
            ["requirement_id"] = StringSchema("Optional requirement id."),
            ["endpoint_id"] = StringSchema("Optional endpoint id."),
            ["module_id"] = StringSchema("Optional module id.")
        });
        AddTool(tools, "get_project_test_deployment", "Return the selected test runtime environment deployment configuration and container port mappings for a project.", new JsonObject
        {
            ["project_id"] = StringSchema("Project id.")
        }, ["project_id"]);
        AddTool(tools, "list_task_hall", "List task hall items visible to the authenticated AgentSprint user.", TaskFilterSchema());
        AddTool(tools, "list_my_tasks", "List tasks assigned to the authenticated AgentSprint user.", new JsonObject());
        AddTool(tools, "get_task_prompt", "Return the task prompt, task detail, requirement detail, and AgentSprint Skill pack by task id for local Codex execution.", new JsonObject
        {
            ["task_id"] = StringSchema("Task identifier.")
        }, ["task_id"]);
        AddTool(tools, "complete_my_task", "Mark an assigned task complete after local Codex work is verified.", new JsonObject
        {
            ["task_id"] = StringSchema("Task identifier."),
            ["project_id"] = StringSchema("Optional project id used to resolve next work."),
            ["requirement_id"] = StringSchema("Optional requirement id used to resolve next work."),
            ["owner_device"] = StringSchema("Optional local device or session id used when returning follow-up polling state."),
            ["idle_round"] = IntegerSchema("Current consecutive idle polling round. Default 0.")
        }, ["task_id"]);
        AddTool(tools, "assign_task", "Assign a task to a developer through AgentSprint.", new JsonObject
        {
            ["task_id"] = StringSchema("Task identifier."),
            ["assignee_id"] = StringSchema("Developer user id.")
        }, ["task_id", "assignee_id"]);
        AddTool(tools, "get_next_work", "Return the highest-priority next work item for the current Codex user without changing platform state.", NextWorkSchema());
        AddTool(tools, "claim_next_work", "Claim the highest-priority next work item when a supported claim action exists.", NextWorkSchema());
        AddTool(tools, "list_test_plans", "List test plans by project or requirement.", ProjectRequirementFilterSchema());
        AddTool(tools, "list_bugs", "List bugs by project or requirement.", ProjectRequirementFilterSchema());
        AddTool(tools, "list_my_open_bugs", "List open or fixing bugs for requirements completed by the current Codex user.", ProjectRequirementFilterSchema());
        AddTool(tools, "claim_bug", "Claim a bug for fixing through AgentSprint.", new JsonObject
        {
            ["bug_id"] = StringSchema("Bug identifier."),
            ["owner_device"] = StringSchema("Optional local device or session id.")
        }, ["bug_id"]);
        AddTool(tools, "fix_bug", "Mark a claimed bug fixed after local verification.", new JsonObject
        {
            ["bug_id"] = StringSchema("Bug identifier.")
        }, ["bug_id"]);
        AddTool(tools, "append_agent_event", "Append a local agent event to the MCP bridge audit stream.", new JsonObject
        {
            ["event_type"] = StringSchema("Event type."),
            ["payload"] = new JsonObject { ["type"] = "object" }
        });
        AddTool(tools, "heartbeat", "Send a local Codex heartbeat.", new JsonObject
        {
            ["session_id"] = StringSchema("Session id."),
            ["status"] = StringSchema("Session status."),
            ["current_task"] = StringSchema("Current task id."),
            ["idle_round"] = IntegerSchema("Current consecutive idle polling round. Default 0.")
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

    private static JsonObject CreateDeliveryOption(string name, string usage)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["usage"] = usage
        };
    }

    private static JsonArray CreateMcpToolGuideTools()
    {
        return new JsonArray
        {
            CreateToolGuideItem("register_session", "Register local Codex session and authenticate against AgentSprint.", [], ["project_code", "device_code", "workspace_path", "username", "password"], ["session_id", "project_code", "device_code", "workspace_path", "api_base_url", "user"]),
            CreateToolGuideItem("get_mcp_tool_guide", "Return this AgentSprint MCP usage guide.", [], ["format"], ["format", "purpose", "recommended_flow", "next_work_priority", "delivery_options", "safety_rules", "tools", "return_shapes", "prompt_snippet"]),
            CreateToolGuideItem("get_agent_skill_pack", "Return required skills, backend rules, frontend rules, and verification commands.", [], ["project_code"], ["project_code", "workspace_path", "required_skills", "backend_rules", "frontend_rules", "verification_commands"]),
            CreateToolGuideItem("get_project_bootstrap", "Return project-level context for Codex startup.", [], ["project_code"], ["project", "project_code", "project_id", "workspace_path", "api_base_url", "current_user", "skill_pack", "requirements", "tasks"]),
            CreateToolGuideItem("get_runtime_environment", "Resolve runtime environment configuration by project, task, or requirement id.", [], ["project_id", "project_code", "task_id", "requirement_id", "endpoint_id", "module_id"], ["project", "task", "requirement", "selected_environment", "environments", "test_url", "resolution"]),
            CreateToolGuideItem("get_project_test_deployment", "Return selected project test-environment deployment details and container mappings.", ["project_id"], [], ["project", "project_id", "selected_environment", "deployment", "containers", "test_url", "notice", "resolution"]),
            CreateToolGuideItem("list_task_hall", "List task-hall items visible to the current user.", [], ["project_id", "requirement_id", "assignee_id", "status", "primary_only"], ["SprintDevelopmentTaskResult[]"]),
            CreateToolGuideItem("list_my_tasks", "List tasks assigned to the current user.", [], [], ["SprintDevelopmentTaskResult[]"]),
            CreateToolGuideItem("get_task_prompt", "Return task prompt, task detail, requirement detail, and skill pack by task id.", ["task_id"], [], ["task_id", "task_prompt", "task_detail", "requirement_detail", "skill_pack", "workspace_path", "codex_instruction"]),
            CreateToolGuideItem("complete_my_task", "Mark current task complete after local verification and return recommended next work.", ["task_id"], ["project_id", "requirement_id", "primary_only", "owner_device"], ["completed_task", "next_work"]),
            CreateToolGuideItem("assign_task", "Assign a task to a developer.", ["task_id", "assignee_id"], [], ["SprintDevelopmentTaskResult"]),
            CreateToolGuideItem("get_next_work", "Return next recommended work without changing platform state.", [], ["project_id", "requirement_id", "owner_device", "primary_only", "idle_round", "session_id"], ["kind", "reason", "item", "claim_supported", "claim_note", "polling", "session"]),
            CreateToolGuideItem("claim_next_work", "Claim the next recommended work when a supported claim action exists.", [], ["project_id", "requirement_id", "owner_device", "primary_only", "idle_round", "session_id"], ["kind", "reason", "item", "claim", "polling", "session"]),
            CreateToolGuideItem("list_test_plans", "List test plans.", [], ["project_id", "requirement_id"], ["TestPlanResult[]"]),
            CreateToolGuideItem("list_bugs", "List bugs by project or requirement.", [], ["project_id", "requirement_id"], ["SprintBugResult[]"]),
            CreateToolGuideItem("list_my_open_bugs", "List open or fixing bugs under requirements completed by the current user.", [], ["project_id", "requirement_id"], ["SprintBugResult[]"]),
            CreateToolGuideItem("claim_bug", "Claim a bug for fixing.", ["bug_id"], ["owner_device"], ["SprintTaskLeaseResult"]),
            CreateToolGuideItem("fix_bug", "Mark a claimed bug fixed after local verification.", ["bug_id"], [], ["SprintBugResult"]),
            CreateToolGuideItem("append_agent_event", "Append local Codex event audit data. Current implementation returns an acknowledgement only.", [], ["event_type", "payload"], ["accepted", "event_type", "payload", "recorded_at"]),
            CreateToolGuideItem("heartbeat", "Send local Codex heartbeat and return session/polling state. Current implementation returns local protocol state only and is not a push listener.", [], ["session_id", "status", "current_task", "idle_round"], ["accepted", "session_id", "status", "current_task", "recorded_at", "polling", "session"]),
            CreateToolGuideItem("close_session", "Close local Codex MCP session. Current implementation returns an acknowledgement only.", [], ["session_id"], ["accepted", "session_id", "closed_at"])
        };
    }

    private static JsonObject CreateToolGuideItem(
        string name,
        string purpose,
        IReadOnlyList<string> requiredParameters,
        IReadOnlyList<string> optionalParameters,
        IReadOnlyList<string> returns)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["purpose"] = purpose,
            ["required_parameters"] = new JsonArray(requiredParameters.Select<string, JsonNode?>(value => JsonValue.Create(value)).ToArray()),
            ["optional_parameters"] = new JsonArray(optionalParameters.Select<string, JsonNode?>(value => JsonValue.Create(value)).ToArray()),
            ["returns"] = new JsonArray(returns.Select<string, JsonNode?>(value => JsonValue.Create(value)).ToArray())
        };
    }

    private static JsonObject CreateMcpToolGuideReturnShapes()
    {
        return new JsonObject
        {
            ["SprintDevelopmentTaskResult"] = new JsonArray
            {
                "id",
                "projectId",
                "requirementId",
                "endpointId",
                "moduleId",
                "title",
                "description",
                "status",
                "priority",
                "assigneeId",
                "assignedBy",
                "createdBy",
                "prompt",
                "assignedAt",
                "startedAt",
                "completedAt",
                "updateTime",
                "createTime"
            },
            ["SprintBugResult"] = new JsonArray
            {
                "id",
                "projectId",
                "requirementId",
                "testPlanId",
                "testExecutionId",
                "title",
                "description",
                "environment",
                "severity",
                "status",
                "createdBy",
                "developerId",
                "fixedAt",
                "createTime"
            },
            ["SprintTaskLeaseResult"] = new JsonArray
            {
                "id",
                "projectId",
                "targetType",
                "targetId",
                "ownerId",
                "ownerDevice",
                "leaseToken",
                "status",
                "expiresAt",
                "completedAt",
                "createTime"
            },
            ["ProjectTestDeploymentInfo"] = new JsonArray
            {
                "project",
                "project_id",
                "selected_environment",
                "deployment",
                "containers",
                "test_url",
                "notice",
                "resolution"
            },
            ["next_work"] = new JsonArray
            {
                "kind",
                "reason",
                "item",
                "claim_supported",
                "claim_note",
                "claim",
                "polling",
                "session"
            },
            ["polling"] = new JsonArray
            {
                "should_continue",
                "strategy",
                "base_interval_seconds",
                "step_seconds",
                "max_interval_seconds",
                "idle_round",
                "next_interval_seconds",
                "next_poll_after"
            },
            ["session"] = new JsonArray
            {
                "session_id",
                "status",
                "offline_requested",
                "stop_reason"
            }
        };
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

    private static JsonObject NextWorkSchema()
    {
        var schema = ProjectRequirementFilterSchema();
        schema["owner_device"] = StringSchema("Optional local device or session id used when claiming bug work.");
        schema["session_id"] = StringSchema("Optional local Codex session id used in returned session state.");
        schema["idle_round"] = IntegerSchema("Current consecutive idle polling round. Default 0.");
        schema["primary_only"] = new JsonObject
        {
            ["type"] = "boolean",
            ["description"] = "Default true. Return pending task-hall items where the current Codex user is a primary developer."
        };
        return schema;
    }

    private static JsonObject ProjectRequirementFilterSchema()
    {
        return new JsonObject
        {
            ["project_id"] = StringSchema("Optional project id."),
            ["requirement_id"] = StringSchema("Optional requirement id."),
            ["endpoint_id"] = StringSchema("Optional endpoint id used to keep next work within the same project endpoint."),
            ["module_id"] = StringSchema("Optional module id.")
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

    private static JsonObject IntegerSchema(string description)
    {
        return new JsonObject
        {
            ["type"] = "integer",
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

    private static string? GetString(JsonNode? node, string name)
    {
        return node is JsonObject jsonObject && jsonObject.TryGetPropertyValue(name, out var value)
            ? value?.GetValue<string>()
            : null;
    }

    private static bool GetBoolean(JsonObject arguments, string name, bool defaultValue)
    {
        return arguments.TryGetPropertyValue(name, out var value) && value is not null
            ? value.GetValue<bool>()
            : defaultValue;
    }

    private static int GetInteger(JsonObject arguments, string name, int defaultValue)
    {
        return arguments.TryGetPropertyValue(name, out var value) && value is not null
            ? value.GetValue<int>()
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

    private static JsonNode? FindById(JsonNode? items, string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || items is not JsonArray itemArray)
        {
            return null;
        }

        return itemArray
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetString(item, "id"), id, StringComparison.Ordinal))
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

    private static JsonObject CreateDeploymentInfo(JsonNode? project, JsonNode? environment)
    {
        return new JsonObject
        {
            ["frontend_url"] = GetString(environment, "frontendUrl") ??
                GetString(project, "testEnvironmentUrl"),
            ["api_base_url"] = GetString(environment, "apiBaseUrl"),
            ["frontend_proxy_api_url"] = GetString(environment, "frontendProxyApiUrl"),
            ["mcp_endpoint"] = GetString(environment, "mcpEndpoint"),
            ["server_ips"] = SplitStringValues(GetString(environment, "serverIps")),
            ["deploy_root"] = GetString(environment, "deployRoot"),
            ["docker_directory"] = GetString(environment, "dockerDirectory"),
            ["remote_package_path"] = GetString(environment, "remotePackagePath"),
            ["compose_file_path"] = GetString(environment, "composeFilePath"),
            ["local_package_paths"] = SplitStringValues(GetString(environment, "localPackagePaths"))
        };
    }

    private static JsonArray SplitStringValues(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : new JsonArray(value.Split([',', ';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(item => JsonValue.Create(item))
                .ToArray<JsonNode?>());
    }

    private static JsonArray CloneArray(JsonNode? node)
    {
        var result = new JsonArray();
        if (node is not JsonArray array)
        {
            return result;
        }

        foreach (var item in array)
        {
            result.Add(item?.DeepClone());
        }

        return result;
    }

    private static int CountActiveContainers(JsonArray containers)
    {
        return containers
            .OfType<JsonObject>()
            .Count(container => GetInteger(container, "status", 0) == 1);
    }

    private async Task<JsonNode?> ResolveTaskByIdAsync(string taskId, CancellationToken cancellationToken)
    {
        var myTasks = await _client.ListMyTasksAsync(cancellationToken);
        var task = FindById(myTasks, taskId);
        if (task is not null)
        {
            return task;
        }

        var taskHall = await _client.ListTaskHallAsync(null, null, null, null, false, cancellationToken);
        return FindById(taskHall, taskId);
    }

    private async Task<JsonNode?> ResolveOptionalTaskAsync(string? taskId, CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(taskId)
            ? null
            : await ResolveTaskByIdAsync(taskId, cancellationToken);
    }

    private static JsonNode? SelectRuntimeEnvironment(JsonNode? environments, string? preferredEnvironmentId)
    {
        if (environments is not JsonArray environmentArray)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferredEnvironmentId))
        {
            var preferred = environmentArray
                .OfType<JsonObject>()
                .FirstOrDefault(environment => GetString(environment, "id") == preferredEnvironmentId);
            if (preferred is not null)
            {
                return preferred.DeepClone();
            }
        }

        return environmentArray
            .OfType<JsonObject>()
            .OrderByDescending(environment => GetString(environment, "environmentType") == "test")
            .ThenBy(environment => GetInteger(environment, "sort", 0))
            .FirstOrDefault()
            ?.DeepClone();
    }

    private static string? ResolveRuntimeEnvironmentUrl(JsonNode? environment)
    {
        return GetString(environment, "frontendUrl") ??
            GetString(environment, "apiBaseUrl") ??
            GetString(environment, "mcpEndpoint");
    }

    private async Task<JsonObject> BuildNextWorkAsync(
        JsonObject arguments,
        bool claimWork,
        CancellationToken cancellationToken)
    {
        var projectId = GetString(arguments, "project_id");
        var requirementId = GetString(arguments, "requirement_id");
        var endpointId = GetString(arguments, "endpoint_id");
        var sessionState = CreateSessionState(arguments, "idle");
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return CreateNoNextWorkResult(
                "No project_id was provided, so AgentSprint will not poll or claim work across projects.",
                arguments,
                sessionState);
        }

        var ownerDevice = ResolveOwnerDevice(arguments);
        var currentUser = await _client.GetCurrentUserAsync(cancellationToken);
        var currentUserId = GetString(currentUser, "id") ?? GetString(currentUser, "userId");
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            throw new InvalidOperationException("Current user id is required to resolve next work.");
        }

        var relatedBugs = await ResolveNextWorkBugsAsync(
            projectId,
            requirementId,
            endpointId,
            currentUserId,
            cancellationToken);
        var bug = FirstObject(relatedBugs);
        if (bug is not null)
        {
            var result = CreateNextWorkResult("bug", "Related open bug has priority over new development work.", bug, arguments, sessionState);
            if (claimWork && GetString(bug, "status") == "open")
            {
                result["claim"] = await _client.ClaimBugAsync(
                    RequireString(bug, "id"),
                    ownerDevice,
                    cancellationToken);
            }

            return result;
        }

        var myTasks = await _client.ListMyTasksAsync(cancellationToken);
        var activeTask = FirstObject(FilterMyPendingTasks(
            myTasks,
            projectId,
            requirementId,
            null));
        activeTask ??= string.IsNullOrWhiteSpace(requirementId)
            ? null
            : FirstObject(FilterMyPendingTasks(
                myTasks,
                projectId,
                null,
                endpointId));
        if (activeTask is not null)
        {
            var result = CreateNextWorkResult("task", "Incomplete task from the current user's task list is ready to continue.", activeTask, arguments, sessionState);
            if (claimWork)
            {
                result["claim"] = await _client.ClaimDevelopmentTaskAsync(
                    RequireString(activeTask, "id"),
                    ownerDevice,
                    cancellationToken);
            }
            else
            {
                result["claim_supported"] = true;
                result["claim_note"] = "Call claim_next_work before starting this task so AgentSprint can create a session lease and avoid another Codex window taking the same task.";
            }

            return result;
        }

        var pendingTasks = await _client.ListTaskHallAsync(
            projectId,
            requirementId,
            null,
            "pending_assign",
            GetBoolean(arguments, "primary_only", true),
            cancellationToken);
        var pendingTask = FirstObject(pendingTasks);
        if (pendingTask is null && !string.IsNullOrWhiteSpace(requirementId))
        {
            pendingTasks = await _client.ListTaskHallAsync(
                projectId,
                null,
                null,
                "pending_assign",
                GetBoolean(arguments, "primary_only", true),
                cancellationToken);
            pendingTask = FirstObject(FilterPendingTaskHallByEndpoint(pendingTasks, projectId, endpointId));
        }

        if (pendingTask is not null)
        {
            var result = CreateNextWorkResult("task", "Pending task-hall item can be assigned by a task manager.", pendingTask, arguments, sessionState);
            if (claimWork)
            {
                var assignedTask = await _client.AssignTaskAsync(
                    RequireString(pendingTask, "id"),
                    currentUserId,
                    cancellationToken);
                result["assignment"] = assignedTask?.DeepClone();
                result["claim"] = await _client.ClaimDevelopmentTaskAsync(
                    RequireString(pendingTask, "id"),
                    ownerDevice,
                    cancellationToken);
            }
            else
            {
                result["claim_supported"] = true;
                result["claim_note"] = "Call claim_next_work to assign this task to the current Codex user when the platform permits self-assignment.";
            }

            return result;
        }

        return CreateNoNextWorkResult(
            "No related open bug, incomplete current-user task, or pending task-hall item was found in the current project.",
            arguments,
            sessionState);
    }

    private static JsonObject EnsureNextWorkProjectContext(JsonObject arguments, JsonNode? task)
    {
        var result = (JsonObject)arguments.DeepClone();
        result["project_id"] = GetString(task, "projectId") ?? GetString(arguments, "project_id");
        result["requirement_id"] = GetString(task, "requirementId") ?? GetString(arguments, "requirement_id");
        result["endpoint_id"] = GetString(task, "endpointId") ?? GetString(arguments, "endpoint_id");
        result["module_id"] = GetString(task, "moduleId") ?? GetString(arguments, "module_id");
        return result;
    }

    private async Task<JsonArray> ResolveRelatedBugsAsync(
        string? projectId,
        string? requirementId,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        HashSet<string>? relatedRequirementIds = null;
        if (!string.IsNullOrWhiteSpace(requirementId))
        {
            relatedRequirementIds = new HashSet<string>(StringComparer.Ordinal) { requirementId };
        }
        else
        {
            var myTasks = await _client.ListMyTasksAsync(cancellationToken);
            relatedRequirementIds = CollectRequirementIds(
                myTasks,
                currentUserId,
                includeCompletedOnly: true,
                projectId);
        }

        var allBugs = await _client.ListBugsAsync(projectId, null, cancellationToken);
        return FilterBugs(allBugs, projectId, relatedRequirementIds, includeFixingOwnedBy: currentUserId);
    }

    private async Task<JsonArray> ResolveNextWorkBugsAsync(
        string projectId,
        string? preferredRequirementId,
        string? endpointId,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var allBugs = await _client.ListBugsAsync(projectId, null, cancellationToken);
        var requirements = string.IsNullOrWhiteSpace(endpointId)
            ? null
            : await _client.ListRequirementsAsync(projectId, cancellationToken);
        return FilterProjectBugs(allBugs, requirements, projectId, preferredRequirementId, endpointId, currentUserId);
    }

    private static JsonObject CreateNextWorkResult(
        string kind,
        string reason,
        JsonObject item,
        JsonObject arguments,
        JsonObject sessionState)
    {
        return new JsonObject
        {
            ["kind"] = kind,
            ["reason"] = reason,
            ["item"] = item.DeepClone(),
            ["scope"] = CreateNextWorkScope(arguments),
            ["polling"] = CreatePollingState(arguments, shouldContinue: true),
            ["session"] = sessionState.DeepClone()
        };
    }

    private static JsonObject CreateNoNextWorkResult(
        string reason,
        JsonObject arguments,
        JsonObject sessionState)
    {
        return new JsonObject
        {
            ["kind"] = "none",
            ["reason"] = reason,
            ["item"] = null,
            ["scope"] = CreateNextWorkScope(arguments),
            ["polling"] = CreatePollingState(arguments, shouldContinue: true),
            ["session"] = sessionState.DeepClone()
        };
    }

    private static JsonObject CreateNextWorkScope(JsonObject arguments)
    {
        return new JsonObject
        {
            ["project_id"] = GetString(arguments, "project_id"),
            ["requirement_id"] = GetString(arguments, "requirement_id"),
            ["endpoint_id"] = GetString(arguments, "endpoint_id"),
            ["module_id"] = GetString(arguments, "module_id")
        };
    }

    private static JsonObject CreatePollingState(JsonObject arguments, bool shouldContinue)
    {
        var idleRound = Math.Max(0, GetInteger(arguments, "idle_round", 0));
        var nextIntervalSeconds = Math.Min(
            PollingBaseIntervalSeconds + (idleRound * PollingStepSeconds),
            PollingMaxIntervalSeconds);
        return new JsonObject
        {
            ["should_continue"] = shouldContinue,
            ["strategy"] = "linear",
            ["base_interval_seconds"] = PollingBaseIntervalSeconds,
            ["step_seconds"] = PollingStepSeconds,
            ["max_interval_seconds"] = PollingMaxIntervalSeconds,
            ["idle_round"] = idleRound,
            ["next_interval_seconds"] = nextIntervalSeconds,
            ["next_poll_after"] = DateTimeOffset.UtcNow.AddSeconds(nextIntervalSeconds)
        };
    }

    private static JsonObject CreateSessionState(JsonObject arguments, string defaultStatus)
    {
        return new JsonObject
        {
            ["session_id"] = GetString(arguments, "session_id"),
            ["status"] = GetString(arguments, "status") ?? defaultStatus,
            ["offline_requested"] = false,
            ["stop_reason"] = null
        };
    }

    private static string? ResolveOwnerDevice(JsonObject arguments)
    {
        return GetString(arguments, "owner_device") ?? GetString(arguments, "session_id");
    }

    private static JsonObject? FirstObject(JsonNode? node)
    {
        return node is JsonArray array ? array.OfType<JsonObject>().FirstOrDefault() : null;
    }

    private static JsonArray FilterTasks(
        JsonNode? tasks,
        string? projectId,
        string? requirementId,
        string? assigneeId,
        HashSet<string> statuses)
    {
        var result = new JsonArray();
        if (tasks is not JsonArray taskArray)
        {
            return result;
        }

        foreach (var task in taskArray.OfType<JsonObject>())
        {
            var status = GetString(task, "status");
            if ((string.IsNullOrWhiteSpace(projectId) || GetString(task, "projectId") == projectId) &&
                (string.IsNullOrWhiteSpace(requirementId) || GetString(task, "requirementId") == requirementId) &&
                (string.IsNullOrWhiteSpace(assigneeId) || GetString(task, "assigneeId") == assigneeId) &&
                status is not null &&
                statuses.Contains(status))
            {
                result.Add(task.DeepClone());
            }
        }

        return result;
    }

    private static JsonArray FilterMyPendingTasks(
        JsonNode? tasks,
        string? projectId,
        string? requirementId,
        string? endpointId)
    {
        var result = new JsonArray();
        if (tasks is not JsonArray taskArray)
        {
            return result;
        }

        foreach (var task in taskArray.OfType<JsonObject>()
            .Where(task => IsTaskInScope(task, projectId, requirementId, endpointId))
            .Select(task => new
            {
                Task = task,
                Priority = GetMyTaskStatusPriority(GetString(task, "status"))
            })
            .Where(item => item.Priority is not null)
            .OrderBy(item => item.Priority))
        {
            result.Add(task.Task.DeepClone());
        }

        return result;
    }

    private static JsonArray FilterPendingTaskHallByEndpoint(
        JsonNode? tasks,
        string? projectId,
        string? endpointId)
    {
        var result = new JsonArray();
        if (tasks is not JsonArray taskArray)
        {
            return result;
        }

        foreach (var task in taskArray.OfType<JsonObject>()
            .Where(task => IsTaskInScope(task, projectId, null, endpointId)))
        {
            result.Add(task.DeepClone());
        }

        return result;
    }

    private static bool IsTaskInScope(JsonObject task, string? projectId, string? requirementId, string? endpointId = null)
    {
        return (string.IsNullOrWhiteSpace(projectId) || GetString(task, "projectId") == projectId) &&
            (string.IsNullOrWhiteSpace(requirementId) || GetString(task, "requirementId") == requirementId) &&
            (string.IsNullOrWhiteSpace(endpointId) || GetString(task, "endpointId") == endpointId);
    }

    private static int? GetMyTaskStatusPriority(string? status)
    {
        return status is not null && MyTaskStatusPriority.TryGetValue(status, out var priority)
            ? priority
            : null;
    }

    private static JsonArray FilterBugs(
        JsonNode? bugs,
        string? projectId,
        HashSet<string> requirementIds,
        string includeFixingOwnedBy)
    {
        var result = new JsonArray();
        if (bugs is not JsonArray bugArray)
        {
            return result;
        }

        foreach (var bug in bugArray.OfType<JsonObject>())
        {
            var bugRequirementId = GetString(bug, "requirementId");
            var bugProjectId = GetString(bug, "projectId");
            var bugStatus = GetString(bug, "status");
            var developerId = GetString(bug, "developerId");
            var isCurrentProject = string.IsNullOrWhiteSpace(projectId) || bugProjectId == projectId;
            var isRelatedRequirement = bugRequirementId is not null && requirementIds.Contains(bugRequirementId);
            var isOpen = bugStatus == "open";
            var isMyFixingBug = bugStatus == "fixing" && developerId == includeFixingOwnedBy;
            if (isCurrentProject && isRelatedRequirement && (isOpen || isMyFixingBug))
            {
                result.Add(bug.DeepClone());
            }
        }

        return result;
    }

    private static JsonArray FilterProjectBugs(
        JsonNode? bugs,
        JsonNode? requirements,
        string projectId,
        string? preferredRequirementId,
        string? endpointId,
        string includeFixingOwnedBy)
    {
        var result = new JsonArray();
        if (bugs is not JsonArray bugArray)
        {
            return result;
        }

        foreach (var bug in bugArray.OfType<JsonObject>()
            .Where(bug =>
            {
                var bugStatus = GetString(bug, "status");
                var developerId = GetString(bug, "developerId");
                return GetString(bug, "projectId") == projectId &&
                    IsBugInEndpointScope(bug, requirements, endpointId) &&
                    (bugStatus == "open" || (bugStatus == "fixing" && developerId == includeFixingOwnedBy));
            })
            .OrderByDescending(bug =>
                !string.IsNullOrWhiteSpace(preferredRequirementId) &&
                GetString(bug, "requirementId") == preferredRequirementId))
        {
            result.Add(bug.DeepClone());
        }

        return result;
    }

    private static bool IsBugInEndpointScope(JsonObject bug, JsonNode? requirements, string? endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return true;
        }

        var directEndpointId = GetString(bug, "endpointId");
        if (!string.IsNullOrWhiteSpace(directEndpointId))
        {
            return directEndpointId == endpointId;
        }

        var requirement = FindById(requirements, GetString(bug, "requirementId"));
        return GetString(requirement, "endpointId") == endpointId;
    }

    private static HashSet<string> CollectRequirementIds(
        JsonNode? tasks,
        string currentUserId,
        bool includeCompletedOnly,
        string? projectId = null)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (tasks is not JsonArray taskArray)
        {
            return result;
        }

        foreach (var task in taskArray.OfType<JsonObject>())
        {
            var requirementId = GetString(task, "requirementId");
            if (string.IsNullOrWhiteSpace(requirementId))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(projectId) && GetString(task, "projectId") != projectId)
            {
                continue;
            }

            var assigneeId = GetString(task, "assigneeId");
            var status = GetString(task, "status");
            if (assigneeId == currentUserId && (!includeCompletedOnly || status == "completed"))
            {
                result.Add(requirementId);
            }
        }

        return result;
    }
}
