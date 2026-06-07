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
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "get_agent_skill_pack");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "get_project_bootstrap");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "get_task_prompt");
        Assert.Contains(tools.OfType<JsonObject>(), tool => tool["name"]?.GetValue<string>() == "complete_my_task");
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
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-AgentSprint-Api-Base-Url"] = "http://agentsprint-api.internal:5000";
        httpContext.Request.Headers.Authorization = "Bearer agent-token";

        var options = AgentSprintMcpOptions.FromHttpContext(httpContext);

        Assert.Equal("http://agentsprint-api.internal:5000/", options.ApiBaseUrl.ToString());
        Assert.Equal("agent-token", options.AgentToken);
    }

    private static AgentSprintMcpServer CreateServer()
    {
        var options = new AgentSprintMcpOptions(
            new Uri("http://localhost:5000"),
            "developer",
            "123456",
            null,
            "F:\\AI\\AgentSprint");
        var client = new AgentSprintApiClient(options, new HttpClient(new NotFoundHandler())
        {
            BaseAddress = options.ApiBaseUrl
        });

        return new AgentSprintMcpServer(client, options);
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
}
