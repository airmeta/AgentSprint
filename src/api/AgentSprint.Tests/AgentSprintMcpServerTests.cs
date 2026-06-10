using System.Text.Json.Nodes;

using AgentSprint.Mcp;
using Microsoft.AspNetCore.Http;

namespace AgentSprint.Tests;

public sealed class AgentSprintMcpServerTests
{
    [Fact]
    public async Task Initialize_ReturnsToolCapability()
    {
        var server = CreateServer();

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""",
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("2.0", response["jsonrpc"]?.GetValue<string>());
        Assert.Equal("agentsprint-mcp", response["result"]?["serverInfo"]?["name"]?.GetValue<string>());
        Assert.NotNull(response["result"]?["capabilities"]?["tools"]);
    }

    [Fact]
    public async Task ToolsList_ExposesCodexAutomationTools()
    {
        var server = CreateServer();

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""",
            CancellationToken.None);

        var tools = response?["result"]?["tools"]?.AsArray();
        Assert.NotNull(tools);
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "get_mcp_tool_guide");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "get_agent_skill_pack");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "get_project_bootstrap");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "get_project_test_deployment");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "get_task_prompt");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "complete_my_task");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "get_next_work");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "claim_next_work");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "list_my_open_bugs");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "claim_bug");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "fix_bug");
    }

    [Fact]
    public async Task ToolsList_IgnoresUtf8BomPrefix()
    {
        var server = CreateServer();

        var response = await server.HandleLineAsync(
            "\uFEFF" + """{"jsonrpc":"2.0","id":22,"method":"tools/list","params":{}}""",
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(22, response["id"]?.GetValue<int>());
        Assert.NotNull(response["result"]?["tools"]);
    }

    [Fact]
    public async Task GetAgentSkillPack_ReturnsRequiredSkillsAndVerificationCommands()
    {
        var server = CreateServer();

        var response = await server.HandleLineAsync(
            """
            {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_agent_skill_pack","arguments":{"project_code":"FINAL-20260607003037"}}}
            """,
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Contains(
            structured["required_skills"]!.AsArray().Select(item => item?.GetValue<string>()),
            value => value == "air-cloud-quality-gate");
        Assert.Contains(
            structured["verification_commands"]!.AsArray().Select(item => item?.GetValue<string>()),
            value => value is not null && value.Contains("dotnet test", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetMcpToolGuide_ReturnsToolUsageGuideAndReturnShapes()
    {
        var server = CreateServer();

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":33,"method":"tools/call","params":{"name":"get_mcp_tool_guide","arguments":{"format":"full"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("full", structured["format"]?.GetValue<string>());
        Assert.Contains(
            structured["tools"]!.AsArray().OfType<JsonObject>(),
            tool => tool["name"]?.GetValue<string>() == "claim_next_work");
        Assert.NotNull(structured["return_shapes"]?["SprintDevelopmentTaskResult"]);
        Assert.NotNull(structured["prompt_snippet"]);
    }

    [Fact]
    public async Task GetNextWork_ReturnsRelatedOpenBugBeforeAssignedTask()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "completed"),
                CreateTask("task-2", "project-1", "req-2", "dev-1", "assigned")
            },
            Bugs = new JsonArray
            {
                CreateBug("bug-1", "project-1", "req-1", "open", null),
                CreateBug("bug-2", "project-1", "req-2", "open", null)
            }
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"get_next_work","arguments":{"project_id":"project-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("bug", structured["kind"]?.GetValue<string>());
        Assert.Equal("bug-1", structured["item"]?["id"]?.GetValue<string>());
        Assert.Equal("project-1", structured["scope"]?["project_id"]?.GetValue<string>());
        Assert.Equal(30, structured["polling"]?["next_interval_seconds"]?.GetValue<int>());
        Assert.False(structured["session"]?["offline_requested"]?.GetValue<bool>());
    }

    [Fact]
    public async Task GetNextWork_ReturnsPendingAssignedTaskFromMyTasks()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "pending_assign")
            },
            Bugs = [],
            TaskHall = []
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":15,"method":"tools/call","params":{"name":"get_next_work","arguments":{"project_id":"project-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task", structured["kind"]?.GetValue<string>());
        Assert.Equal("task-1", structured["item"]?["id"]?.GetValue<string>());
        Assert.Equal("pending_assign", structured["item"]?["status"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetNextWork_PrioritizesInProgressMyTaskBeforePendingAssignedTask()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "pending_assign"),
                CreateTask("task-2", "project-1", "req-2", "dev-1", "in_progress")
            },
            Bugs = [],
            TaskHall = []
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":16,"method":"tools/call","params":{"name":"get_next_work","arguments":{"project_id":"project-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task", structured["kind"]?.GetValue<string>());
        Assert.Equal("task-2", structured["item"]?["id"]?.GetValue<string>());
        Assert.Equal("in_progress", structured["item"]?["status"]?.GetValue<string>());
    }

    [Fact]
    public async Task ClaimNextWork_CreatesLeaseForCurrentUserTask()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "assigned")
            },
            Bugs = [],
            TaskHall = [],
            ClaimTaskResponse = new JsonObject
            {
                ["id"] = "lease-task-1",
                ["targetType"] = "development_task",
                ["targetId"] = "task-1",
                ["ownerId"] = "dev-1",
                ["ownerDevice"] = "window-1"
            }
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":17,"method":"tools/call","params":{"name":"claim_next_work","arguments":{"project_id":"project-1","session_id":"window-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task", structured["kind"]?.GetValue<string>());
        Assert.Equal("lease-task-1", structured["claim"]?["id"]?.GetValue<string>());
        Assert.Equal("development_task", structured["claim"]?["targetType"]?.GetValue<string>());
        Assert.Equal("/mvp/tasks/task-1/claim", apiHandler.LastRequestPath);
    }

    [Fact]
    public async Task GetNextWork_ReturnsLinearPollingStateWhenNoWorkExists()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            MyTasks = [],
            Bugs = [],
            TaskHall = []
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":11,"method":"tools/call","params":{"name":"get_next_work","arguments":{"session_id":"session-1","idle_round":7}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("none", structured["kind"]?.GetValue<string>());
        Assert.True(structured["polling"]?["should_continue"]?.GetValue<bool>());
        Assert.Equal("linear", structured["polling"]?["strategy"]?.GetValue<string>());
        Assert.Equal(180, structured["polling"]?["next_interval_seconds"]?.GetValue<int>());
        Assert.Equal("session-1", structured["session"]?["session_id"]?.GetValue<string>());
        Assert.False(structured["session"]?["offline_requested"]?.GetValue<bool>());
    }

    [Fact]
    public async Task GetNextWork_DoesNotCrossProjectWhenProjectContextIsMissing()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            MyTasks = new JsonArray
            {
                CreateTask("task-2", "project-2", "req-2", "dev-1", "assigned")
            },
            Bugs = new JsonArray
            {
                CreateBug("bug-2", "project-2", "req-2", "open", null)
            },
            TaskHall = new JsonArray
            {
                CreateTask("task-3", "project-3", "req-3", string.Empty, "pending_assign")
            }
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":18,"method":"tools/call","params":{"name":"get_next_work","arguments":{"session_id":"session-1","idle_round":1}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("none", structured["kind"]?.GetValue<string>());
        Assert.Contains("No project_id was provided", structured["reason"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Null(structured["scope"]?["project_id"]?.GetValue<string>());
        Assert.Equal(60, structured["polling"]?["next_interval_seconds"]?.GetValue<int>());
    }

    [Fact]
    public async Task ClaimNextWork_ClaimsOpenBugWhenClaimActionExists()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            MyTasks = new JsonArray { CreateTask("task-1", "project-1", "req-1", "dev-1", "completed") },
            Bugs = new JsonArray { CreateBug("bug-1", "project-1", "req-1", "open", null) },
            ClaimBugResponse = new JsonObject
            {
                ["id"] = "lease-1",
                ["targetType"] = "bug",
                ["targetId"] = "bug-1",
                ["ownerId"] = "dev-1"
            }
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"claim_next_work","arguments":{"project_id":"project-1","owner_device":"devbox-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("bug", structured["kind"]?.GetValue<string>());
        Assert.Equal("lease-1", structured["claim"]?["id"]?.GetValue<string>());
        Assert.Equal("/mvp/bugs/bug-1/claim", apiHandler.LastRequestPath);
    }

    [Fact]
    public async Task ListMyOpenBugs_ReturnsOnlyOpenBugsForCompletedRequirements()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "completed"),
                CreateTask("task-2", "project-1", "req-2", "dev-1", "assigned")
            },
            Bugs = new JsonArray
            {
                CreateBug("bug-1", "project-1", "req-1", "open", null),
                CreateBug("bug-2", "project-1", "req-2", "open", null),
                CreateBug("bug-3", "project-1", "req-1", "closed", null)
            }
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"list_my_open_bugs","arguments":{"project_id":"project-1"}}}""",
            CancellationToken.None);

        var bugs = response?["result"]?["structuredContent"]?.AsArray();
        Assert.NotNull(bugs);
        Assert.Single(bugs);
        Assert.Equal("bug-1", bugs[0]?["id"]?.GetValue<string>());
    }

    [Fact]
    public async Task ClaimNextWork_AssignsPendingTaskToCurrentUserWhenNoBugExists()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            MyTasks = [],
            Bugs = [],
            TaskHall = new JsonArray { CreateTask("task-1", "project-1", "req-1", string.Empty, "pending_assign") },
            AssignTaskResponse = CreateTask("task-1", "project-1", "req-1", "dev-1", "assigned")
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"claim_next_work","arguments":{"project_id":"project-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task", structured["kind"]?.GetValue<string>());
        Assert.Equal("assigned", structured["assignment"]?["status"]?.GetValue<string>());
        Assert.Equal("development_task", structured["claim"]?["targetType"]?.GetValue<string>());
        Assert.Equal("/mvp/tasks/task-1/claim", apiHandler.LastRequestPath);
    }

    [Fact]
    public async Task CompleteMyTask_ReturnsCompletedTaskAndNextWork()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            CompletedTask = CreateTask("task-1", "project-1", "req-1", "dev-1", "completed"),
            MyTasks = new JsonArray { CreateTask("task-1", "project-1", "req-1", "dev-1", "completed") },
            Bugs = new JsonArray { CreateBug("bug-1", "project-1", "req-1", "open", null) }
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"complete_my_task","arguments":{"task_id":"task-1","project_id":"project-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task-1", structured["completed_task"]?["id"]?.GetValue<string>());
        Assert.Equal("bug", structured["next_work"]?["kind"]?.GetValue<string>());
        Assert.Equal("bug-1", structured["next_work"]?["item"]?["id"]?.GetValue<string>());
        Assert.NotNull(structured["next_work"]?["polling"]);
        Assert.NotNull(structured["next_work"]?["session"]);
    }

    [Fact]
    public async Task CompleteMyTask_ContinuesOnlyWithinCompletedTaskProjectWhenProjectArgumentIsMissing()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            CompletedTask = CreateTask("task-1", "project-1", "req-1", "dev-1", "completed"),
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "completed"),
                CreateTask("task-2", "project-2", "req-2", "dev-1", "assigned")
            },
            Bugs = [],
            TaskHall = []
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":19,"method":"tools/call","params":{"name":"complete_my_task","arguments":{"task_id":"task-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task-1", structured["completed_task"]?["id"]?.GetValue<string>());
        Assert.Equal("none", structured["next_work"]?["kind"]?.GetValue<string>());
        Assert.Contains("current project", structured["next_work"]?["reason"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("project-1", structured["next_work"]?["scope"]?["project_id"]?.GetValue<string>());
    }

    [Fact]
    public async Task CompleteMyTask_UsesCompletedTaskProjectWhenProjectArgumentIsProjectCode()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            CompletedTask = CreateTask("task-1", "project-id-1", "req-1", "dev-1", "completed", "endpoint-admin"),
            MyTasks = new JsonArray
            {
                CreateTask("task-2", "project-id-1", "req-2", "dev-1", "assigned", "endpoint-admin")
            },
            Bugs = [],
            TaskHall = []
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":25,"method":"tools/call","params":{"name":"complete_my_task","arguments":{"task_id":"task-1","project_id":"PROJECT-CODE"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task", structured["next_work"]?["kind"]?.GetValue<string>());
        Assert.Equal("task-2", structured["next_work"]?["item"]?["id"]?.GetValue<string>());
        Assert.Equal("project-id-1", structured["next_work"]?["scope"]?["project_id"]?.GetValue<string>());
    }

    [Fact]
    public async Task CompleteMyTask_FallsBackToSameEndpointWhenCompletedRequirementHasNoNextTask()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            CompletedTask = CreateTask("task-1", "project-1", "req-1", "dev-1", "completed", "endpoint-admin"),
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "completed", "endpoint-admin"),
                CreateTask("task-2", "project-1", "req-2", "dev-1", "assigned", "endpoint-admin"),
                CreateTask("task-3", "project-1", "req-3", "dev-1", "in_progress", "endpoint-api")
            },
            Bugs = [],
            TaskHall = []
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":20,"method":"tools/call","params":{"name":"complete_my_task","arguments":{"task_id":"task-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task", structured["next_work"]?["kind"]?.GetValue<string>());
        Assert.Equal("task-2", structured["next_work"]?["item"]?["id"]?.GetValue<string>());
        Assert.Equal("project-1", structured["next_work"]?["scope"]?["project_id"]?.GetValue<string>());
        Assert.Equal("req-1", structured["next_work"]?["scope"]?["requirement_id"]?.GetValue<string>());
        Assert.Equal("endpoint-admin", structured["next_work"]?["scope"]?["endpoint_id"]?.GetValue<string>());
    }

    [Fact]
    public async Task CompleteMyTask_ReturnsSameRequirementTaskBeforeOtherProjectTasks()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            CompletedTask = CreateTask("task-1", "project-1", "req-1", "dev-1", "completed"),
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "completed"),
                CreateTask("task-2", "project-1", "req-2", "dev-1", "in_progress"),
                CreateTask("task-3", "project-1", "req-1", "dev-1", "assigned")
            },
            Bugs = [],
            TaskHall = []
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":21,"method":"tools/call","params":{"name":"complete_my_task","arguments":{"task_id":"task-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task", structured["next_work"]?["kind"]?.GetValue<string>());
        Assert.Equal("task-3", structured["next_work"]?["item"]?["id"]?.GetValue<string>());
        Assert.Equal("assigned", structured["next_work"]?["item"]?["status"]?.GetValue<string>());
    }

    [Fact]
    public async Task CompleteMyTask_ReturnsSameEndpointBugBeforeSameEndpointTask()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            CompletedTask = CreateTask("task-1", "project-1", "req-1", "dev-1", "completed", "endpoint-admin"),
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "completed", "endpoint-admin"),
                CreateTask("task-2", "project-1", "req-1", "dev-1", "assigned", "endpoint-admin")
            },
            Bugs = new JsonArray
            {
                CreateBug("bug-1", "project-1", "req-2", "open", null)
            },
            Requirements = new JsonArray
            {
                CreateRequirement("req-1", "project-1", "endpoint-admin"),
                CreateRequirement("req-2", "project-1", "endpoint-admin")
            },
            TaskHall = []
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":22,"method":"tools/call","params":{"name":"complete_my_task","arguments":{"task_id":"task-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("bug", structured["next_work"]?["kind"]?.GetValue<string>());
        Assert.Equal("bug-1", structured["next_work"]?["item"]?["id"]?.GetValue<string>());
    }

    [Fact]
    public async Task CompleteMyTask_DoesNotUseDifferentEndpointBugOrTaskAsNextWork()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            CompletedTask = CreateTask("task-1", "project-1", "req-1", "dev-1", "completed", "endpoint-admin"),
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "completed", "endpoint-admin"),
                CreateTask("task-2", "project-1", "req-2", "dev-1", "in_progress", "endpoint-api"),
                CreateTask("task-3", "project-1", "req-3", "dev-1", "assigned", "endpoint-admin")
            },
            Bugs = new JsonArray
            {
                CreateBug("bug-1", "project-1", "req-2", "open", null)
            },
            Requirements = new JsonArray
            {
                CreateRequirement("req-1", "project-1", "endpoint-admin"),
                CreateRequirement("req-2", "project-1", "endpoint-api"),
                CreateRequirement("req-3", "project-1", "endpoint-admin")
            },
            TaskHall = []
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":24,"method":"tools/call","params":{"name":"complete_my_task","arguments":{"task_id":"task-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task", structured["next_work"]?["kind"]?.GetValue<string>());
        Assert.Equal("task-3", structured["next_work"]?["item"]?["id"]?.GetValue<string>());
    }

    [Fact]
    public async Task CompleteMyTask_ReturnsSameRequirementBugBeforeOtherProjectBugs()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "dev-1", ["userId"] = "dev-1", ["username"] = "developer" },
            CompletedTask = CreateTask("task-1", "project-1", "req-1", "dev-1", "completed"),
            MyTasks = new JsonArray
            {
                CreateTask("task-1", "project-1", "req-1", "dev-1", "completed")
            },
            Bugs = new JsonArray
            {
                CreateBug("bug-1", "project-1", "req-2", "open", null),
                CreateBug("bug-2", "project-1", "req-1", "open", null)
            },
            TaskHall = []
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":23,"method":"tools/call","params":{"name":"complete_my_task","arguments":{"task_id":"task-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("bug", structured["next_work"]?["kind"]?.GetValue<string>());
        Assert.Equal("bug-2", structured["next_work"]?["item"]?["id"]?.GetValue<string>());
    }

    [Fact]
    public async Task Heartbeat_ReturnsPollingAndSessionState()
    {
        var server = CreateServer();

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":12,"method":"tools/call","params":{"name":"heartbeat","arguments":{"session_id":"session-1","status":"idle","current_task":"task-1","idle_round":2}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.True(structured["accepted"]?.GetValue<bool>());
        Assert.Equal(90, structured["polling"]?["next_interval_seconds"]?.GetValue<int>());
        Assert.Equal("idle", structured["session"]?["status"]?.GetValue<string>());
        Assert.False(structured["session"]?["offline_requested"]?.GetValue<bool>());
    }

    [Fact]
    public async Task GetTaskPrompt_ReturnsTaskAndRequirementDetailsForTaskId()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            MyTasks = new JsonArray { CreateTask("task-1", "project-1", "req-1", "dev-1", "assigned") },
            Requirements = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "req-1",
                    ["projectId"] = "project-1",
                    ["title"] = "Increase optimization suggestions",
                    ["description"] = "Full requirement content returned through MCP"
                }
            },
            TaskPrompt = new JsonObject
            {
                ["taskId"] = "task-1",
                ["prompt"] = "short prompt with task id only"
            }
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"get_task_prompt","arguments":{"task_id":"task-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("task-1", structured["task_id"]?.GetValue<string>());
        Assert.Equal("task-1", structured["task_detail"]?["id"]?.GetValue<string>());
        Assert.Equal("req-1", structured["requirement_detail"]?["id"]?.GetValue<string>());
        Assert.Equal("Full requirement content returned through MCP", structured["requirement_detail"]?["description"]?.GetValue<string>());
        Assert.Equal("task-1", structured["task_prompt"]?["taskId"]?.GetValue<string>());
        Assert.Contains("same task_id", structured["codex_instruction"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetProjectTestDeployment_ReturnsSelectedEnvironmentAndContainers()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            Projects = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "project-1",
                    ["code"] = "AGENTSPRINT",
                    ["testEnvironmentId"] = "env-test",
                    ["testEnvironmentUrl"] = "http://fallback.test"
                }
            },
            RuntimeEnvironments = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "env-test",
                    ["projectId"] = "project-1",
                    ["code"] = "test",
                    ["name"] = "Test",
                    ["environmentType"] = "test",
                    ["frontendUrl"] = "http://192.168.80.101:6010",
                    ["apiBaseUrl"] = "http://192.168.80.101:5000",
                    ["frontendProxyApiUrl"] = "http://192.168.80.101:5000",
                    ["mcpEndpoint"] = "http://192.168.80.101:5010/mcp",
                    ["serverIps"] = "192.168.80.101\r\n192.168.80.102",
                    ["deployRoot"] = "/opt/agentsprint-deploy",
                    ["dockerDirectory"] = "/opt/agentsprint-deploy/docker",
                    ["remotePackagePath"] = "/opt/agentsprint-deploy/agentsprint-docker-deploy.tgz",
                    ["composeFilePath"] = "/opt/agentsprint-deploy/docker/docker-compose.yml",
                    ["localPackagePaths"] = "F:\\AI\\AgentSprint\\agentsprint-docker-deploy.tgz",
                    ["sort"] = 1,
                    ["status"] = 1
                }
            },
            RuntimeEnvironmentContainers = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "container-admin",
                    ["runtimeEnvironmentId"] = "env-test",
                    ["name"] = "agentsprint-admin",
                    ["containerType"] = 1,
                    ["serverIp"] = "192.168.80.101",
                    ["hostPort"] = 6010,
                    ["containerPort"] = 80,
                    ["protocol"] = "tcp",
                    ["prompt"] = "Deploy the admin service with the service script.",
                    ["deployScript"] = "docker compose up -d agentsprint-admin",
                    ["status"] = 1
                },
                new JsonObject
                {
                    ["id"] = "container-api",
                    ["runtimeEnvironmentId"] = "env-test",
                    ["name"] = "agentsprint-api",
                    ["containerType"] = 2,
                    ["serverIp"] = "192.168.80.101",
                    ["hostPort"] = 5000,
                    ["containerPort"] = 5000,
                    ["protocol"] = "tcp",
                    ["status"] = 0
                }
            }
        };
        var server = CreateServer(apiHandler);

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":25,"method":"tools/call","params":{"name":"get_project_test_deployment","arguments":{"project_id":"project-1"}}}""",
            CancellationToken.None);

        var structured = response?["result"]?["structuredContent"];
        Assert.NotNull(structured);
        Assert.Equal("project-1", structured["project_id"]?.GetValue<string>());
        Assert.Equal("env-test", structured["selected_environment"]?["id"]?.GetValue<string>());
        Assert.Equal("http://192.168.80.101:6010", structured["deployment"]?["frontend_url"]?.GetValue<string>());
        Assert.Equal("/opt/agentsprint-deploy/docker/docker-compose.yml", structured["deployment"]?["compose_file_path"]?.GetValue<string>());
        Assert.Equal("192.168.80.101", structured["deployment"]?["server_ips"]?[0]?.GetValue<string>());
        Assert.Equal(2, structured["containers"]?.AsArray().Count);
        Assert.Equal("agentsprint-admin", structured["containers"]?[0]?["name"]?.GetValue<string>());
        Assert.Equal("Deploy the admin service with the service script.", structured["containers"]?[0]?["prompt"]?.GetValue<string>());
        Assert.Equal("docker compose up -d agentsprint-admin", structured["containers"]?[0]?["deployScript"]?.GetValue<string>());
        Assert.Equal("你必须按照提示词和脚本来进行部署系统,如果脚本出现异常,直接提示用户异常原因是什么,不要尝试更换方式去绕过脚本", structured["notice"]?.GetValue<string>());
        Assert.Equal(2, structured["resolution"]?["container_count"]?.GetValue<int>());
        Assert.Equal(1, structured["resolution"]?["active_container_count"]?.GetValue<int>());
        Assert.Equal("/system/runtime-environment-containers?runtimeEnvironmentId=env-test", apiHandler.LastRequestPath);
    }

    [Fact]
    public async Task UnknownTool_ReturnsJsonRpcError()
    {
        var server = CreateServer();

        var response = await server.HandleLineAsync(
            """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"missing_tool","arguments":{}}}""",
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(-32000, response["error"]?["code"]?.GetValue<int>());
        Assert.Contains("missing_tool", response["error"]?["message"]?.GetValue<string>());
    }

    [Fact]
    public async Task MalformedJson_ReturnsParseError()
    {
        var server = CreateServer();

        var response = await server.HandleLineAsync("{bad json", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(-32700, response["error"]?["code"]?.GetValue<int>());
    }

    [Fact]
    public void FromHttpContext_UsesExplicitApiBaseUrlHeaderWhenProvided()
    {
        var token = new string('a', 64);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-AgentSprint-Api-Base-Url"] = "http://agentsprint-api.internal:5000";
        httpContext.Request.Headers.Authorization = $"Bearer {token}";

        var options = AgentSprintMcpOptions.FromHttpContext(httpContext);

        Assert.Equal("http://agentsprint-api.internal:5000/", options.ApiBaseUrl.ToString());
        Assert.Equal(token, options.AgentToken);
    }

    [Fact]
    public void FromHttpContext_TreatsJwtBearerAsAccessToken()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer header.payload.signature";

        var options = AgentSprintMcpOptions.FromHttpContext(httpContext);

        Assert.Equal("header.payload.signature", options.AccessToken);
        Assert.Null(options.AgentToken);
        Assert.True(options.RequireRequestAuthentication);
    }

    [Fact]
    public void FromHttpContext_ReadsCustomAccessTokenHeaderValue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-AgentSprint-Access-Token"] = "access-token";

        var options = AgentSprintMcpOptions.FromHttpContext(httpContext);

        Assert.Equal("access-token", options.AccessToken);
        Assert.Null(options.AgentToken);
        Assert.Contains("X-AgentSprint-Access-Token", options.RequestHeaderNames ?? []);
    }

    [Fact]
    public void FromHttpContext_TreatsOpaqueBearerAsAgentTokenWhenLengthMatches()
    {
        var token = new string('a', 64);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = $"Bearer {token}";

        var options = AgentSprintMcpOptions.FromHttpContext(httpContext);

        Assert.Equal(token, options.AgentToken);
        Assert.Null(options.AccessToken);
        Assert.True(options.RequireRequestAuthentication);
    }

    [Fact]
    public async Task RemoteHttpOptions_RejectMissingRequestAuthenticationBeforeDefaultLogin()
    {
        var options = new AgentSprintMcpOptions(
            new Uri("http://localhost:5000"),
            "developer",
            "123456",
            null,
            null,
            "F:\\AI\\AgentSprint",
            true,
            null);
        var client = new AgentSprintApiClient(options, new HttpClient(new NotFoundHandler())
        {
            BaseAddress = options.ApiBaseUrl
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetCurrentUserAsync(CancellationToken.None));

        Assert.Contains("missing Authorization Bearer", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterSession_KeepsRemoteAccessToken()
    {
        var apiHandler = new FakeAgentSprintApiHandler
        {
            CurrentUser = new JsonObject { ["id"] = "admin-1", ["userId"] = "admin-1", ["username"] = "admin" }
        };
        var options = new AgentSprintMcpOptions(
            new Uri("http://localhost:5000"),
            "developer",
            "123456",
            "jwt-token",
            null,
            "F:\\AI\\AgentSprint",
            true,
            ["Authorization"]);
        var client = new AgentSprintApiClient(options, new HttpClient(apiHandler)
        {
            BaseAddress = options.ApiBaseUrl
        });

        var session = await client.RegisterSessionAsync(
            "project-code",
            null,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.Equal("admin", session?["user"]?["username"]?.GetValue<string>());
        Assert.Equal("/user/info", apiHandler.LastRequestPath);
    }

    private static AgentSprintMcpServer CreateServer(HttpMessageHandler? handler = null)
    {
        var options = new AgentSprintMcpOptions(
            new Uri("http://localhost:5000"),
            "developer",
            "123456",
            null,
            null,
            "F:\\AI\\AgentSprint",
            false,
            null);
        var client = new AgentSprintApiClient(options, new HttpClient(handler ?? new NotFoundHandler())
        {
            BaseAddress = options.ApiBaseUrl
        });

        return new AgentSprintMcpServer(client, options);
    }

    private static JsonObject CreateTask(
        string id,
        string projectId,
        string requirementId,
        string assigneeId,
        string status,
        string? endpointId = null)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["projectId"] = projectId,
            ["requirementId"] = requirementId,
            ["endpointId"] = endpointId,
            ["assigneeId"] = assigneeId,
            ["status"] = status,
            ["priority"] = 1
        };
    }

    private static JsonObject CreateRequirement(
        string id,
        string projectId,
        string? endpointId)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["projectId"] = projectId,
            ["endpointId"] = endpointId,
            ["title"] = id,
            ["status"] = "developing"
        };
    }

    private static JsonObject CreateBug(
        string id,
        string projectId,
        string requirementId,
        string status,
        string? developerId)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["projectId"] = projectId,
            ["requirementId"] = requirementId,
            ["status"] = status,
            ["developerId"] = developerId,
            ["title"] = id
        };
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"code":404,"message":"not mocked","data":null}""")
            });
        }
    }

    private sealed class FakeAgentSprintApiHandler : HttpMessageHandler
    {
        public JsonObject CurrentUser { get; init; } = new()
        {
            ["id"] = "dev-1",
            ["userId"] = "dev-1",
            ["username"] = "developer"
        };

        public JsonArray MyTasks { get; init; } = [];

        public JsonArray Projects { get; init; } = [];

        public JsonArray Bugs { get; init; } = [];

        public JsonArray TaskHall { get; init; } = [];

        public JsonArray Requirements { get; init; } = [];

        public JsonArray RuntimeEnvironments { get; init; } = [];

        public JsonArray RuntimeEnvironmentContainers { get; init; } = [];

        public JsonObject CompletedTask { get; init; } = CreateTask("task-1", "project-1", "req-1", "dev-1", "completed");

        public JsonObject TaskPrompt { get; init; } = new()
        {
            ["taskId"] = "task-1",
            ["prompt"] = "task prompt"
        };

        public JsonObject ClaimBugResponse { get; init; } = new()
        {
            ["id"] = "lease-1",
            ["targetType"] = "bug"
        };

        public JsonObject AssignTaskResponse { get; init; } = CreateTask("task-1", "project-1", "req-1", "dev-1", "assigned");

        public JsonObject ClaimTaskResponse { get; init; } = new()
        {
            ["id"] = "lease-task-1",
            ["targetType"] = "development_task",
            ["targetId"] = "task-1"
        };

        public string? LastRequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestPath = request.RequestUri?.PathAndQuery;
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            JsonNode? data = path switch
            {
                "/auth/login" => new JsonObject { ["accessToken"] = "access-token" },
                "/user/info" => CurrentUser.DeepClone(),
                "/mvp/projects" => Projects.DeepClone(),
                "/mvp/tasks/my" => MyTasks.DeepClone(),
                "/mvp/tasks" => TaskHall.DeepClone(),
                "/mvp/requirements" => Requirements.DeepClone(),
                "/mvp/bugs" => Bugs.DeepClone(),
                "/system/runtime-environments" => RuntimeEnvironments.DeepClone(),
                "/system/runtime-environment-containers" => RuntimeEnvironmentContainers.DeepClone(),
                var value when value.StartsWith("/mvp/tasks/", StringComparison.Ordinal) &&
                    value.EndsWith("/prompt", StringComparison.Ordinal) => TaskPrompt.DeepClone(),
                var value when value.StartsWith("/mvp/tasks/", StringComparison.Ordinal) &&
                    value.EndsWith("/complete", StringComparison.Ordinal) => CompletedTask.DeepClone(),
                var value when value.StartsWith("/mvp/tasks/", StringComparison.Ordinal) &&
                    value.EndsWith("/assign", StringComparison.Ordinal) => AssignTaskResponse.DeepClone(),
                var value when value.StartsWith("/mvp/tasks/", StringComparison.Ordinal) &&
                    value.EndsWith("/claim", StringComparison.Ordinal) => ClaimTaskResponse.DeepClone(),
                var value when value.StartsWith("/mvp/bugs/", StringComparison.Ordinal) &&
                    value.EndsWith("/claim", StringComparison.Ordinal) => ClaimBugResponse.DeepClone(),
                var value when value.StartsWith("/mvp/bugs/", StringComparison.Ordinal) &&
                    value.EndsWith("/fix", StringComparison.Ordinal) => new JsonObject { ["id"] = "bug-1", ["status"] = "fixed_ready_regression" },
                _ => null
            };

            if (data is null)
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                {
                    Content = new StringContent("""{"code":404,"message":"not mocked","data":null}""")
                });
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(new JsonObject
                {
                    ["code"] = 0,
                    ["message"] = "ok",
                    ["data"] = data
                }.ToJsonString())
            });
        }
    }
}
