using System.Security.Claims;

using AgentSprint.Entry;
using AgentSprint.Entry.Controllers;
using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Tests;

public sealed class AgileMvpControllerTests
{
    [Fact]
    public async Task CreateProject_UsesAuthenticatedUser()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "pm-100", ["pm"]);

        var result = await controller.CreateProject(
            CreateProjectRequest("AGENT", "AgentSprint"));

        var response = Assert.IsType<ApiResponse<SprintProjectResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("pm-100", service.LastUserId);
        Assert.Equal("AGENT", response.Data?.Code);
    }

    [Fact]
    public async Task UpdateProject_UsesRouteProjectId()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "pm-100", ["pm"]);

        var result = await controller.UpdateProject(
            "project-100",
            new UpdateSprintProjectRequest(
                "AgentSprint Admin",
                "https://example.com/agentsprint.git",
                "http://localhost:5999",
                "Project detail",
                "Vue 3 / Vite",
                ".NET 10",
                "manager-1",
                ["pm-1"],
                ["dev-1"],
                ["tester-1"],
                "arch-1"));

        var response = Assert.IsType<ApiResponse<SprintProjectResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("project-100", service.LastProjectId);
        Assert.Equal("AgentSprint Admin", response.Data?.Name);
    }

    [Fact]
    public async Task CreateSkill_UsesAuthenticatedUser()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "admin-100", ["super"]);

        var result = await controller.CreateSkill(
            new CreateSprintSkillRequest("AIR-CLOUD", "Air.Cloud gate", "Follow delivery rules."));

        var response = Assert.IsType<ApiResponse<SprintSkillResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("admin-100", service.LastUserId);
        Assert.Equal("AIR-CLOUD", response.Data?.Code);
    }

    [Fact]
    public async Task UpdateSkill_UsesRouteSkillId()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "admin-100", ["super"]);

        var result = await controller.UpdateSkill(
            "skill-100",
            new UpdateSprintSkillRequest(
                "Updated skill",
                "Updated content",
                "Updated description",
                SprintSkillStatuses.Disabled));

        var response = Assert.IsType<ApiResponse<SprintSkillResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("skill-100", response.Data?.Id);
        Assert.Equal(SprintSkillStatuses.Disabled, response.Data?.Status);
    }

    [Fact]
    public async Task CreateRequirement_ReturnsBadRequestWhenServiceRejectsPayload()
    {
        var service = new CapturingAgileMvpService
        {
            CreateRequirementException = new InvalidOperationException("ProjectId and Title are required.")
        };
        var controller = CreateController(service, "po-100", ["pm"]);

        var result = await controller.CreateRequirement(
            new CreateSprintRequirementRequest("", "", null, null));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<SprintRequirementResult>>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("ProjectId and Title are required.", response.Message);
    }

    [Fact]
    public async Task ClaimRequirement_UsesRouteRequirementAndAuthenticatedUser()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "dev-100", ["developer"]);

        var result = await controller.ClaimRequirement(
            "req-100",
            new ClaimSprintTaskRequest("devbox-100"));

        var response = Assert.IsType<ApiResponse<SprintTaskLeaseResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("dev-100", service.LastUserId);
        Assert.Equal("req-100", service.LastRequirementId);
        Assert.Equal("devbox-100", response.Data?.OwnerDevice);
    }

    [Fact]
    public async Task CloseBug_UsesRouteBugId()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "tester-100", ["tester"]);

        var result = await controller.CloseBug("bug-100");

        var response = Assert.IsType<ApiResponse<SprintBugResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("bug-100", response.Data?.Id);
        Assert.Equal(SprintBugStatuses.Closed, response.Data?.Status);
    }

    [Fact]
    public async Task CompleteDevelopmentTask_UsesAuthenticatedAssignee()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "dev-100", ["developer"]);

        var result = await controller.CompleteDevelopmentTask("task-100");

        var response = Assert.IsType<ApiResponse<SprintDevelopmentTaskResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("dev-100", service.LastUserId);
        Assert.Equal(SprintDevelopmentTaskStatuses.Completed, response.Data?.Status);
        Assert.NotNull(response.Data?.CompletedAt);
    }

    [Fact]
    public async Task AssignDevelopmentTask_ReturnsForbiddenForDeveloperRole()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "dev-100", ["developer"]);

        var result = await controller.AssignDevelopmentTask(
            "task-100",
            new AssignSprintDevelopmentTaskRequest("dev-1"));

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
        var response = Assert.IsType<ApiResponse<SprintDevelopmentTaskResult>>(forbidden.Value);
        Assert.Equal(403, response.Code);
    }

    [Fact]
    public async Task AssignDevelopmentTask_AllowsProductManagerRole()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "pm-100", ["pm"]);

        var result = await controller.AssignDevelopmentTask(
            "task-100",
            new AssignSprintDevelopmentTaskRequest("dev-1"));

        var response = Assert.IsType<ApiResponse<SprintDevelopmentTaskResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("pm-100", service.LastUserId);
    }

    [Fact]
    public async Task ListDevelopmentTasks_RestrictsDeveloperToOwnAssignedTasks()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "dev-100", ["developer"]);

        var result = await controller.ListDevelopmentTasks("project-100", "req-100", "dev-200", "assigned");

        var response = Assert.IsType<ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("project-100", service.LastTaskProjectId);
        Assert.Equal("req-100", service.LastTaskRequirementId);
        Assert.Equal("dev-100", service.LastTaskAssigneeId);
        Assert.Equal("assigned", service.LastTaskStatus);
    }

    [Fact]
    public async Task ListDevelopmentTasks_AllowsProjectManagerToViewAllTasks()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "manager-100", ["project_manager"]);

        var result = await controller.ListDevelopmentTasks("project-100", "req-100", "dev-200", "pending_assign");

        var response = Assert.IsType<ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("project-100", service.LastTaskProjectId);
        Assert.Equal("req-100", service.LastTaskRequirementId);
        Assert.Equal("dev-200", service.LastTaskAssigneeId);
        Assert.Equal("pending_assign", service.LastTaskStatus);
        Assert.True(service.LastUsedGlobalTaskList);
        Assert.False(service.LastUsedParticipatingTaskList);
    }

    [Fact]
    public async Task ListDevelopmentTasks_AllowsSuperAdministratorToViewAllTasks()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "admin-100", ["super"]);

        var result = await controller.ListDevelopmentTasks("project-100", "req-100", "dev-200", "pending_assign");

        var response = Assert.IsType<ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("project-100", service.LastTaskProjectId);
        Assert.Equal("req-100", service.LastTaskRequirementId);
        Assert.Equal("dev-200", service.LastTaskAssigneeId);
        Assert.Equal("pending_assign", service.LastTaskStatus);
        Assert.True(service.LastUsedGlobalTaskList);
        Assert.False(service.LastUsedParticipatingTaskList);
    }

    [Fact]
    public async Task ListRequirementReviews_UsesRouteRequirementId()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "po-100", ["pm"]);

        var result = await controller.ListRequirementReviews("req-100");

        var response = Assert.IsType<ApiResponse<IReadOnlyList<SprintRequirementReviewResult>>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("req-100", service.LastRequirementId);
        Assert.Single(response.Data!);
        Assert.Equal(SprintRequirementReviewStatuses.Rejected, response.Data![0].Status);
    }

    [Fact]
    public async Task UpdateRequirement_UsesRouteRequirementAndAuthenticatedUser()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "po-100", ["pm"]);

        var result = await controller.UpdateRequirement(
            "req-100",
            new UpdateSprintRequirementRequest("Updated", "Body", 2, "arch-1"));

        var response = Assert.IsType<ApiResponse<SprintRequirementResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("req-100", service.LastRequirementId);
        Assert.Equal("po-100", service.LastUserId);
        Assert.Equal("Updated", response.Data?.Title);
    }

    [Fact]
    public async Task SubmitRequirementReview_UsesRouteRequirementAndAuthenticatedUser()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "po-100", ["pm"]);

        var result = await controller.SubmitRequirementReview(
            "req-100",
            new SubmitSprintRequirementReviewRequest(["arch-1"]));

        var response = Assert.IsType<ApiResponse<SprintRequirementResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("req-100", service.LastRequirementId);
        Assert.Equal("po-100", service.LastUserId);
    }

    [Fact]
    public async Task VoidRequirement_UsesRouteRequirementAndAuthenticatedUser()
    {
        var service = new CapturingAgileMvpService();
        var controller = CreateController(service, "po-100", ["pm"]);

        var result = await controller.VoidRequirement("req-100");

        var response = Assert.IsType<ApiResponse<SprintRequirementResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("req-100", service.LastRequirementId);
        Assert.Equal("po-100", service.LastUserId);
    }

    private static AgileMvpController CreateController(
        IAgileMvpService service,
        string userId,
        IReadOnlyList<string>? roles = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        claims.AddRange((roles ?? ["super"]).Select(role => new Claim(ClaimTypes.Role, role)));

        return new AgileMvpController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            claims,
                            "unit-test"))
                }
            }
        };
    }

    private static CreateSprintProjectRequest CreateProjectRequest(string code, string name)
    {
        return new CreateSprintProjectRequest(
            code,
            name,
            "https://example.com/agentsprint.git",
            "http://localhost:5999",
            "Project detail",
            "Vue 3 / Vite",
            ".NET 10",
            "manager-1",
            ["pm-1"],
            ["dev-1"],
            ["tester-1"],
            "arch-1");
    }
}

internal sealed class CapturingAgileMvpService : IAgileMvpService
{
    public string? LastProjectId { get; private set; }

    public string? LastRequirementId { get; private set; }

    public string? LastTaskAssigneeId { get; private set; }

    public string? LastTaskProjectId { get; private set; }

    public string? LastTaskRequirementId { get; private set; }

    public string? LastTaskStatus { get; private set; }

    public string? LastUserId { get; private set; }

    public bool LastUsedGlobalTaskList { get; private set; }

    public bool LastUsedParticipatingTaskList { get; private set; }

    public InvalidOperationException? CreateRequirementException { get; init; }

    public Task<SprintProjectResult> CreateProjectAsync(CreateSprintProjectRequest request, string userId)
    {
        LastUserId = userId;
        return Task.FromResult(new SprintProjectResult(
            "project-1",
            request.Code,
            request.Name,
            request.RepositoryUrl,
            request.TestEnvironmentUrl,
            request.Description,
            request.FrontendTechStack,
            request.BackendTechStack,
            request.ProjectManagerId,
            request.ProductManagerIds ?? [],
            request.DeveloperIds ?? [],
            request.ArchitectId,
            SprintProjectStatuses.Active,
            userId,
            DateTime.UtcNow,
            request.TesterIds ?? []));
    }

    public Task<IReadOnlyList<SprintProjectResult>> ListProjectsAsync()
    {
        IReadOnlyList<SprintProjectResult> projects = [];
        return Task.FromResult(projects);
    }

    public Task<SprintProjectResult> UpdateProjectAsync(string id, UpdateSprintProjectRequest request)
    {
        LastProjectId = id;
        return Task.FromResult(new SprintProjectResult(
            id,
            "AGENT",
            request.Name,
            request.RepositoryUrl,
            request.TestEnvironmentUrl,
            request.Description,
            request.FrontendTechStack,
            request.BackendTechStack,
            request.ProjectManagerId,
            request.ProductManagerIds ?? [],
            request.DeveloperIds ?? [],
            request.ArchitectId,
            SprintProjectStatuses.Active,
            "pm-100",
            DateTime.UtcNow,
            request.TesterIds ?? []));
    }

    public Task<SprintSkillResult> CreateSkillAsync(CreateSprintSkillRequest request, string userId)
    {
        LastUserId = userId;
        return Task.FromResult(new SprintSkillResult(
            "skill-1",
            request.Code,
            request.Name,
            request.Description,
            request.Content,
            SprintSkillStatuses.Active,
            userId,
            DateTime.UtcNow));
    }

    public Task<IReadOnlyList<SprintSkillResult>> ListSkillsAsync(bool activeOnly = false)
    {
        IReadOnlyList<SprintSkillResult> skills =
        [
            new SprintSkillResult(
                "skill-1",
                "AIR-CLOUD",
                "Air.Cloud delivery gate",
                "Repository delivery gate",
                "Follow Air.Cloud delivery rules.",
                SprintSkillStatuses.Active,
                "admin-1",
                DateTime.UtcNow)
        ];
        return Task.FromResult(skills);
    }

    public Task<SprintSkillResult> UpdateSkillAsync(string id, UpdateSprintSkillRequest request)
    {
        return Task.FromResult(new SprintSkillResult(
            id,
            "AIR-CLOUD",
            request.Name,
            request.Description,
            request.Content,
            request.Status ?? SprintSkillStatuses.Active,
            "admin-1",
            DateTime.UtcNow));
    }

    public Task<SprintProjectEndpointResult> CreateProjectEndpointAsync(
        CreateSprintProjectEndpointRequest request,
        string userId)
    {
        LastProjectId = request.ProjectId;
        LastUserId = userId;
        return Task.FromResult(new SprintProjectEndpointResult(
            "endpoint-1",
            request.ProjectId,
            request.Code,
            request.Name,
            request.Type,
            request.OwnerId,
            request.DeveloperIds ?? [],
            request.TesterIds ?? [],
            request.Sort ?? 0,
            SprintProjectEndpointStatuses.Active,
            userId,
            DateTime.UtcNow,
            request.SkillIds ?? []));
    }

    public Task<IReadOnlyList<SprintProjectEndpointResult>> ListProjectEndpointsAsync(string? projectId)
    {
        LastProjectId = projectId;
        IReadOnlyList<SprintProjectEndpointResult> endpoints = [];
        return Task.FromResult(endpoints);
    }

    public Task<SprintProjectEndpointResult> UpdateProjectEndpointAsync(
        string id,
        UpdateSprintProjectEndpointRequest request)
    {
        return Task.FromResult(new SprintProjectEndpointResult(
            id,
            "project-1",
            "WEB",
            request.Name,
            request.Type,
            request.OwnerId,
            request.DeveloperIds ?? [],
            request.TesterIds ?? [],
            request.Sort ?? 0,
            request.Status ?? SprintProjectEndpointStatuses.Active,
            "pm-1",
            DateTime.UtcNow,
            request.SkillIds ?? []));
    }

    public Task<SprintFeatureModuleResult> CreateFeatureModuleAsync(
        CreateSprintFeatureModuleRequest request,
        string userId)
    {
        LastProjectId = request.ProjectId;
        LastUserId = userId;
        return Task.FromResult(new SprintFeatureModuleResult(
            "module-1",
            request.ProjectId,
            request.EndpointId,
            request.Code,
            request.Name,
            request.Description,
            request.OwnerId,
            request.DeveloperIds ?? [],
            request.TesterIds ?? [],
            request.Sort ?? 0,
            SprintFeatureModuleStatuses.Active,
            userId,
            DateTime.UtcNow));
    }

    public Task<IReadOnlyList<SprintFeatureModuleResult>> ListFeatureModulesAsync(
        string? projectId,
        string? endpointId)
    {
        LastProjectId = projectId;
        IReadOnlyList<SprintFeatureModuleResult> modules = [];
        return Task.FromResult(modules);
    }

    public Task<SprintFeatureModuleResult> UpdateFeatureModuleAsync(
        string id,
        UpdateSprintFeatureModuleRequest request)
    {
        return Task.FromResult(new SprintFeatureModuleResult(
            id,
            "project-1",
            "endpoint-1",
            "GENERAL",
            request.Name,
            request.Description,
            request.OwnerId,
            request.DeveloperIds ?? [],
            request.TesterIds ?? [],
            request.Sort ?? 0,
            request.Status ?? SprintFeatureModuleStatuses.Active,
            "pm-1",
            DateTime.UtcNow));
    }

    public Task<SprintRequirementResult> CreateRequirementAsync(
        CreateSprintRequirementRequest request,
        string userId)
    {
        LastUserId = userId;
        if (CreateRequirementException is not null)
        {
            throw CreateRequirementException;
        }

        return Task.FromResult(CreateRequirementResult("req-1", request.ProjectId, request.Title));
    }

    public Task<IReadOnlyList<SprintRequirementResult>> ListRequirementsAsync(string? projectId)
    {
        IReadOnlyList<SprintRequirementResult> requirements = [];
        return Task.FromResult(requirements);
    }

    public Task<SprintRequirementResult> UpdateRequirementAsync(
        string id,
        UpdateSprintRequirementRequest request,
        string userId)
    {
        LastRequirementId = id;
        LastUserId = userId;
        return Task.FromResult(CreateRequirementResult(id, "project-1", request.Title));
    }

    public Task<SprintRequirementResult> SubmitRequirementReviewAsync(
        string id,
        SubmitSprintRequirementReviewRequest request,
        string userId)
    {
        LastRequirementId = id;
        LastUserId = userId;
        return Task.FromResult(CreateRequirementResult(id, "project-1", "Requirement"));
    }

    public Task<IReadOnlyList<SprintRequirementReviewItemResult>> ListMyPendingReviewsAsync(string reviewerId)
    {
        LastUserId = reviewerId;
        IReadOnlyList<SprintRequirementReviewItemResult> reviews = [];
        return Task.FromResult(reviews);
    }

    public Task<IReadOnlyList<SprintRequirementReviewResult>> ListRequirementReviewsAsync(string requirementId)
    {
        LastRequirementId = requirementId;
        IReadOnlyList<SprintRequirementReviewResult> reviews =
        [
            new SprintRequirementReviewResult(
                "review-1",
                "project-1",
                requirementId,
                "arch-1",
                SprintRequirementReviewStatuses.Rejected,
                "Need clearer acceptance criteria.",
                DateTime.UtcNow,
                DateTime.UtcNow)
        ];
        return Task.FromResult(reviews);
    }

    public Task<SprintRequirementFeedbackResult> CreateRequirementFeedbackAsync(
        string requirementId,
        CreateSprintRequirementFeedbackRequest request,
        string userId)
    {
        LastRequirementId = requirementId;
        LastUserId = userId;
        return Task.FromResult(new SprintRequirementFeedbackResult(
            "feedback-1",
            "project-1",
            requirementId,
            request.Title,
            request.Content,
            SprintRequirementFeedbackStatuses.Open,
            userId,
            null,
            null,
            null,
            DateTime.UtcNow));
    }

    public Task<IReadOnlyList<SprintRequirementFeedbackResult>> ListRequirementFeedbackAsync(string requirementId)
    {
        LastRequirementId = requirementId;
        IReadOnlyList<SprintRequirementFeedbackResult> feedback = [];
        return Task.FromResult(feedback);
    }

    public Task<SprintRequirementResult> ConvertRequirementFeedbackAsync(
        string requirementId,
        string feedbackId,
        ConvertSprintRequirementFeedbackRequest request,
        string userId)
    {
        LastRequirementId = requirementId;
        LastUserId = userId;
        return Task.FromResult(CreateRequirementResult("req-follow-up", "project-1", request.Title));
    }

    public Task<SprintFeatureSuggestionResult> CreateFeatureSuggestionAsync(
        CreateSprintFeatureSuggestionRequest request,
        string userId)
    {
        LastProjectId = request.ProjectId;
        LastRequirementId = request.RequirementId;
        LastUserId = userId;
        return Task.FromResult(new SprintFeatureSuggestionResult(
            "suggestion-1",
            request.ProjectId,
            request.EndpointId,
            request.ModuleId,
            request.RequirementId,
            request.Content,
            SprintFeatureSuggestionStatuses.Open,
            userId,
            DateTime.UtcNow));
    }

    public Task<IReadOnlyList<SprintFeatureSuggestionResult>> ListFeatureSuggestionsAsync(
        string? projectId,
        string? moduleId,
        string? requirementId)
    {
        LastProjectId = projectId;
        LastRequirementId = requirementId;
        IReadOnlyList<SprintFeatureSuggestionResult> suggestions = [];
        return Task.FromResult(suggestions);
    }

    public Task<SprintRequirementResult> ApproveRequirementReviewAsync(
        string id,
        string userId,
        DecideSprintRequirementReviewRequest request)
    {
        LastRequirementId = id;
        LastUserId = userId;
        return Task.FromResult(CreateRequirementResult(id, "project-1", "Requirement"));
    }

    public Task<SprintRequirementResult> RejectRequirementReviewAsync(
        string id,
        string userId,
        DecideSprintRequirementReviewRequest request)
    {
        LastRequirementId = id;
        LastUserId = userId;
        return Task.FromResult(CreateRequirementResult(id, "project-1", "Requirement"));
    }

    public Task<SprintRequirementResult> ApproveRequirementAsync(string id, string userId)
    {
        LastRequirementId = id;
        LastUserId = userId;
        return Task.FromResult(CreateRequirementResult(id, "project-1", "Requirement"));
    }

    public Task<SprintRequirementResult> VoidRequirementAsync(string id, string userId)
    {
        LastRequirementId = id;
        LastUserId = userId;
        return Task.FromResult(CreateRequirementResult(id, "project-1", "Requirement"));
    }

    public Task<IReadOnlyList<SprintDevelopmentTaskResult>> DecomposeRequirementAsync(
        string id,
        DecomposeSprintRequirementRequest request,
        string userId)
    {
        LastRequirementId = id;
        LastUserId = userId;
        IReadOnlyList<SprintDevelopmentTaskResult> tasks =
        [
            CreateTaskResult("task-1", id, null)
        ];
        return Task.FromResult(tasks);
    }

    public Task<IReadOnlyList<SprintDevelopmentTaskResult>> ListDevelopmentTasksAsync(
        string? projectId,
        string? requirementId,
        string? assigneeId,
        string? status = null)
    {
        LastUsedGlobalTaskList = true;
        LastUsedParticipatingTaskList = false;
        LastTaskAssigneeId = assigneeId;
        LastTaskProjectId = projectId;
        LastTaskRequirementId = requirementId;
        LastTaskStatus = status;
        IReadOnlyList<SprintDevelopmentTaskResult> tasks = [];
        return Task.FromResult(tasks);
    }

    public Task<IReadOnlyList<SprintDevelopmentTaskResult>> ListParticipatingDevelopmentTasksAsync(
        string? projectId,
        string? requirementId,
        string? assigneeId,
        string? status,
        bool primaryOnly,
        string userId)
    {
        LastUsedParticipatingTaskList = true;
        LastUsedGlobalTaskList = false;
        LastTaskProjectId = projectId;
        LastTaskRequirementId = requirementId;
        LastTaskAssigneeId = assigneeId;
        LastTaskStatus = status;
        LastUserId = userId;
        IReadOnlyList<SprintDevelopmentTaskResult> tasks =
        [
            new SprintDevelopmentTaskResult(
                "task-1",
                projectId ?? "project-1",
                requirementId ?? "req-1",
                null,
                null,
                "Task",
                null,
                status ?? SprintDevelopmentTaskStatuses.PendingAssign,
                1,
                assigneeId,
                null,
                "po-1",
                null,
                null,
                null,
                null,
                null,
                DateTime.UtcNow)
        ];
        return Task.FromResult(tasks);
    }

    public Task<SprintDevelopmentTaskResult> AssignDevelopmentTaskAsync(
        string id,
        AssignSprintDevelopmentTaskRequest request,
        string assignedBy)
    {
        LastUserId = assignedBy;
        return Task.FromResult(CreateTaskResult(id, "req-1", request.AssigneeId, assignedBy));
    }

    public Task<SprintTaskPromptResult> GetDevelopmentTaskPromptAsync(string id, string userId)
    {
        LastUserId = userId;
        return Task.FromResult(new SprintTaskPromptResult(
            id,
            "copy this prompt",
            new SprintTaskPromptSectionResult("MCP 接入配置", "setup", [], []),
            new SprintTaskPromptSectionResult("任务推进提示词", "execute", [], [])));
    }

    public Task<SprintDevelopmentTaskResult> CompleteDevelopmentTaskAsync(string id, string userId)
    {
        LastUserId = userId;
        return Task.FromResult(CreateTaskResult(id, "req-1", userId, "pm-1", SprintDevelopmentTaskStatuses.Completed));
    }

    public Task<SprintTaskLeaseResult> ClaimRequirementAsync(
        string id,
        ClaimSprintTaskRequest request,
        string userId)
    {
        LastRequirementId = id;
        LastUserId = userId;
        return Task.FromResult(CreateLeaseResult(id, SprintTaskTargetTypes.Requirement, userId, request.OwnerDevice));
    }

    public Task<SprintRequirementResult> CompleteRequirementDevelopmentAsync(
        string id,
        CompleteSprintDevelopmentRequest request)
    {
        LastRequirementId = id;
        return Task.FromResult(CreateRequirementResult(id, "project-1", "Requirement"));
    }

    public Task<SprintRequirementResult> StartRequirementTestingAsync(string id)
    {
        LastRequirementId = id;
        return Task.FromResult(CreateRequirementResult(id, "project-1", "Requirement"));
    }

    public Task<SprintRequirementResult> MarkRequirementTestedAsync(string id)
    {
        LastRequirementId = id;
        return Task.FromResult(CreateRequirementResult(id, "project-1", "Requirement"));
    }

    public Task<SprintRequirementResult> CloseRequirementAsync(string id)
    {
        LastRequirementId = id;
        return Task.FromResult(CreateRequirementResult(id, "project-1", "Requirement"));
    }

    public Task<SprintBugResult> CreateBugAsync(CreateSprintBugRequest request, string userId)
    {
        LastUserId = userId;
        return Task.FromResult(new SprintBugResult(
            "bug-1",
            request.ProjectId,
            request.RequirementId,
            request.TestPlanId,
            request.TestExecutionId,
            request.Title,
            request.Description,
            request.Environment ?? "test",
            request.Severity ?? SprintBugSeverities.Major,
            SprintBugStatuses.Open,
            userId,
            null,
            null,
            DateTime.UtcNow));
    }

    public Task<IReadOnlyList<SprintBugResult>> ListBugsAsync(string? projectId, string? requirementId)
    {
        IReadOnlyList<SprintBugResult> bugs = [];
        return Task.FromResult(bugs);
    }

    public Task<SprintTaskLeaseResult> ClaimBugAsync(string id, ClaimSprintTaskRequest request, string userId)
    {
        LastUserId = userId;
        return Task.FromResult(CreateLeaseResult(id, SprintTaskTargetTypes.Bug, userId, request.OwnerDevice));
    }

    public Task<SprintBugResult> FixBugAsync(string id)
    {
        return Task.FromResult(new SprintBugResult(
            id,
            "project-1",
            "req-1",
            null,
            null,
            "Bug",
            null,
            "test",
            SprintBugSeverities.Major,
            SprintBugStatuses.FixedReadyForRegression,
            "tester-1",
            "dev-1",
            DateTime.UtcNow,
            DateTime.UtcNow));
    }

    public Task<SprintBugResult> CloseBugAsync(string id)
    {
        return Task.FromResult(new SprintBugResult(
            id,
            "project-1",
            "req-1",
            null,
            null,
            "Bug",
            null,
            "test",
            SprintBugSeverities.Major,
            SprintBugStatuses.Closed,
            "tester-1",
            "dev-1",
            DateTime.UtcNow,
            DateTime.UtcNow));
    }

    public Task<IReadOnlyList<SprintTaskLeaseResult>> ListActiveLeasesAsync()
    {
        IReadOnlyList<SprintTaskLeaseResult> leases = [];
        return Task.FromResult(leases);
    }

    public Task<SprintMvpSummaryResult> GetSummaryAsync()
    {
        return Task.FromResult(new SprintMvpSummaryResult(0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    private static SprintRequirementResult CreateRequirementResult(string id, string projectId, string title)
    {
        return new SprintRequirementResult(
            id,
            projectId,
            title,
            null,
            SprintRequirementStatuses.PendingReview,
            3,
            "po-1",
            "pm-1,arch-1",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "primary",
            DateTime.UtcNow);
    }

    private static SprintDevelopmentTaskResult CreateTaskResult(
        string id,
        string requirementId,
        string? assigneeId,
        string? assignedBy = null,
        string? status = null)
    {
        var taskStatus = status ??
            (assigneeId is null ? SprintDevelopmentTaskStatuses.PendingAssign : SprintDevelopmentTaskStatuses.Assigned);
        return new SprintDevelopmentTaskResult(
            id,
            "project-1",
            requirementId,
            null,
            null,
            "Task",
            null,
            taskStatus,
            3,
            assigneeId,
            assignedBy,
            "po-1",
            null,
            assigneeId is null ? null : DateTime.UtcNow,
            null,
            taskStatus == SprintDevelopmentTaskStatuses.Completed ? DateTime.UtcNow : null,
            assigneeId is null ? null : DateTime.UtcNow,
            DateTime.UtcNow);
    }

    private static SprintTaskLeaseResult CreateLeaseResult(
        string targetId,
        string targetType,
        string ownerId,
        string? ownerDevice)
    {
        return new SprintTaskLeaseResult(
            "lease-1",
            "project-1",
            targetType,
            targetId,
            ownerId,
            ownerDevice,
            "token-1",
            SprintTaskLeaseStatuses.Active,
            DateTime.UtcNow.AddHours(8),
            null,
            DateTime.UtcNow);
    }
}
