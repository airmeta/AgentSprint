using AgentSprint.Mcp;

using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new
{
    name = "agentsprint-mcp",
    transport = "streamable-http",
    status = "ok"
}));

app.MapMethods("/mcp", ["GET"], () => Results.Ok(new
{
    name = "agentsprint-mcp",
    transport = "streamable-http",
    endpoint = "/mcp"
}));

app.MapPost("/mcp", async (HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var request = await JsonNode.ParseAsync(httpContext.Request.Body, cancellationToken: cancellationToken);
    if (request is null)
    {
        return Results.BadRequest(new
        {
            jsonrpc = "2.0",
            error = new { code = -32600, message = "Invalid MCP request." }
        });
    }

    var options = AgentSprintMcpOptions.FromHttpContext(httpContext);
    var client = new AgentSprintApiClient(options);
    var server = new AgentSprintMcpServer(client, options);
    var response = await server.HandleJsonAsync(request, cancellationToken);
    return response is null ? Results.Accepted() : Results.Json(response);
});

app.Run();
