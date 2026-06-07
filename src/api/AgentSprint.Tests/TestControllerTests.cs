using System.Security.Claims;

using AgentSprint.Entry;
using AgentSprint.Entry.Controllers;
using AgentSprint.Model.Modules.Tests;
using AgentSprint.Model.Modules.Tests.Dtos;
using AgentSprint.Service.Services.TestServices;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Tests;

public sealed class TestControllerTests
{
    [Fact]
    public async Task CreatePlan_UsesAuthenticatedUserAndReturnsOkEnvelope()
    {
        var service = new CapturingTestService();
        var controller = CreateController(service, "user-100");
        var request = new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null);

        var actionResult = await controller.CreatePlan(request);

        var response = Assert.IsType<ApiResponse<TestPlanResult>>(actionResult.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("user-100", service.LastUserId);
        Assert.Equal("REQ-1", response.Data?.RequirementId);
    }

    [Fact]
    public async Task CreatePlan_ReturnsBadRequestWhenServiceRejectsPayload()
    {
        var service = new CapturingTestService
        {
            CreatePlanException = new InvalidOperationException("ProjectId, RequirementId and Name are required.")
        };
        var controller = CreateController(service, "user-100");

        var actionResult = await controller.CreatePlan(
            new CreateTestPlanRequest("", "", "", null, null, null));

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<TestPlanResult>>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("ProjectId, RequirementId and Name are required.", response.Message);
    }

    [Fact]
    public async Task SubmitExecution_UsesRoutePlanIdAndAuthenticatedTester()
    {
        var service = new CapturingTestService();
        var controller = CreateController(service, "tester-100");

        var actionResult = await controller.SubmitExecution(
            "plan-100",
            new SubmitTestExecutionRequest(TestExecutionResults.Passed, "Loaded", "manual", null, null));

        var response = Assert.IsType<ApiResponse<TestExecutionResult>>(actionResult.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("plan-100", service.LastPlanId);
        Assert.Equal("tester-100", service.LastUserId);
        Assert.Equal(TestExecutionResults.Passed, response.Data?.Result);
    }

    [Fact]
    public async Task UpdateExecutionBug_UsesRoutePlanAndExecutionIds()
    {
        var service = new CapturingTestService();
        var controller = CreateController(service, "tester-100");

        var actionResult = await controller.UpdateExecutionBug(
            "plan-100",
            "execution-100",
            new UpdateTestExecutionBugRequest(null, "bug-100"));

        var response = Assert.IsType<ApiResponse<TestExecutionResult>>(actionResult.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("plan-100", service.LastPlanId);
        Assert.Equal("execution-100", service.LastExecutionId);
        Assert.Equal("bug-100", response.Data?.CreatedBugId);
    }

    private static TestController CreateController(ITestService service, string userId)
    {
        return new TestController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, userId)],
                            "unit-test"))
                }
            }
        };
    }
}

internal sealed class CapturingTestService : ITestService
{
    public string? LastPlanId { get; private set; }

    public string? LastExecutionId { get; private set; }

    public string? LastUserId { get; private set; }

    public InvalidOperationException? CreatePlanException { get; init; }

    public Task<TestPlanResult> CreatePlanAsync(CreateTestPlanRequest request, string userId)
    {
        LastUserId = userId;
        if (CreatePlanException is not null)
        {
            throw CreatePlanException;
        }

        return Task.FromResult(new TestPlanResult(
            "plan-1",
            request.ProjectId,
            request.RequirementId,
            request.BugId,
            request.Name,
            request.Environment ?? "test",
            request.TestUrl,
            TestPlanStatuses.Pending,
            userId,
            null,
            null,
            null,
            DateTime.UtcNow));
    }

    public Task<TestPlanResult> StartPlanAsync(string id)
    {
        LastPlanId = id;
        return Task.FromResult(CreatePlanResult(id, TestPlanStatuses.Testing));
    }

    public Task<TestPlanResult> CompletePlanAsync(string id, CompleteTestPlanRequest request)
    {
        LastPlanId = id;
        return Task.FromResult(CreatePlanResult(id, request.Status));
    }

    public Task<IReadOnlyList<TestPlanResult>> ListPlansAsync(string? projectId, string? requirementId)
    {
        IReadOnlyList<TestPlanResult> plans = [CreatePlanResult("plan-1", TestPlanStatuses.Pending)];
        return Task.FromResult(plans);
    }

    public Task<TestExecutionResult> SubmitExecutionAsync(
        string testPlanId,
        SubmitTestExecutionRequest request,
        string userId)
    {
        LastPlanId = testPlanId;
        LastUserId = userId;
        return Task.FromResult(new TestExecutionResult(
            "execution-1",
            testPlanId,
            "REQ-1",
            request.BugId,
            userId,
            request.Result,
            request.ActualResult,
            request.Evidence,
            request.CreatedBugId,
            DateTime.UtcNow,
            DateTime.UtcNow));
    }

    public Task<IReadOnlyList<TestExecutionResult>> ListExecutionsAsync(string testPlanId)
    {
        LastPlanId = testPlanId;
        IReadOnlyList<TestExecutionResult> executions = [];
        return Task.FromResult(executions);
    }

    public Task<TestExecutionResult> UpdateExecutionBugAsync(
        string testPlanId,
        string executionId,
        UpdateTestExecutionBugRequest request)
    {
        LastPlanId = testPlanId;
        LastExecutionId = executionId;
        return Task.FromResult(new TestExecutionResult(
            executionId,
            testPlanId,
            "REQ-1",
            request.BugId,
            "tester-1",
            TestExecutionResults.Failed,
            "Page returned 500",
            null,
            request.CreatedBugId,
            DateTime.UtcNow,
            DateTime.UtcNow));
    }

    private static TestPlanResult CreatePlanResult(string id, string status)
    {
        return new TestPlanResult(
            id,
            "PROJ",
            "REQ-1",
            null,
            "First round",
            "test",
            null,
            status,
            "user-1",
            null,
            null,
            null,
            DateTime.UtcNow);
    }
}
