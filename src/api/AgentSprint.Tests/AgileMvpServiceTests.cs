using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Model.Modules.Tests;
using AgentSprint.Model.Modules.Tests.Domains;
using AgentSprint.Service.Impls.AgileServices;
using AgentSprint.Service.Services.AgileServices;
using AgentSprint.Service.Services.SecurityServices;

using System.Linq.Expressions;

namespace AgentSprint.Tests;

public sealed class AgileMvpServiceTests
{
    [Fact]
    public async Task RequirementLoop_CompletesMinimalHappyPath()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-HAPPY", "MVP happy path", testEnvironmentUrl: "http://localhost:5999"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Create dashboard", "Show loop status", 1),
            "po-1");

        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var lease = await service.ClaimRequirementAsync(
            requirement.Id,
            new ClaimSprintTaskRequest("devbox-1"),
            "dev-1");
        requirement = await service.CompleteRequirementDevelopmentAsync(
            requirement.Id,
            new CompleteSprintDevelopmentRequest("http://localhost:5999/sprint/mvp"));
        requirement = await service.StartRequirementTestingAsync(requirement.Id);
        requirement = await service.MarkRequirementTestedAsync(requirement.Id);
        requirement = await service.CloseRequirementAsync(requirement.Id);

        Assert.Equal(SprintRequirementStatuses.Completed, requirement.Status);
        Assert.NotNull(requirement.ClosedAt);
        Assert.Equal("dev-1", lease.OwnerId);
        Assert.Empty(await service.ListActiveLeasesAsync());
    }

    [Fact]
    public async Task FailedTesting_CreatesBugAndReturnsRequirementToReadyTestAfterFix()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-BUG", "MVP bug path"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Fix failing flow", null, 2),
            "po-1");

        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        await service.ClaimRequirementAsync(requirement.Id, new ClaimSprintTaskRequest(null), "dev-1");
        requirement = await service.CompleteRequirementDevelopmentAsync(
            requirement.Id,
            new CompleteSprintDevelopmentRequest(null));
        requirement = await service.StartRequirementTestingAsync(requirement.Id);

        var bug = await service.CreateBugAsync(
            new CreateSprintBugRequest(
                project.Id,
                requirement.Id,
                "Dashboard crashes",
                "Opening the page returns 500",
                "test",
                "critical",
                null,
                null),
            "tester-1");
        var afterBug = (await service.ListRequirementsAsync(project.Id)).Single();

        Assert.Equal(SprintRequirementStatuses.PendingFix, afterBug.Status);
        Assert.Equal(SprintBugStatuses.Open, bug.Status);
        Assert.Equal(SprintBugSeverities.Critical, bug.Severity);

        await service.ClaimBugAsync(bug.Id, new ClaimSprintTaskRequest("devbox-1"), "dev-1");
        bug = await service.FixBugAsync(bug.Id);
        var afterFix = (await service.ListRequirementsAsync(project.Id)).Single();

        Assert.Equal(SprintBugStatuses.FixedReadyForRegression, bug.Status);
        Assert.Equal(SprintRequirementStatuses.ReadyForTest, afterFix.Status);

        bug = await service.CloseBugAsync(bug.Id);
        var afterClose = (await service.ListRequirementsAsync(project.Id)).Single();

        Assert.Equal(SprintBugStatuses.Closed, bug.Status);
        Assert.Equal("success", afterClose.Health);
    }

    [Fact]
    public async Task CreateProjectAsync_CreatesDefaultAdminEndpoint()
    {
        var endpointDomain = new InMemorySprintProjectEndpointDomain();
        var service = CreateService(endpointDomain: endpointDomain);

        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DEFAULT-ADMIN", "Default admin endpoint"),
            "pm-1");
        var endpoints = await endpointDomain.ListAsync(entity => entity.ProjectId == project.Id);

        var endpoint = Assert.Single(endpoints);
        Assert.Equal("DEFAULT-ADMIN", endpoint.Code);
        Assert.Equal("管理后台", endpoint.Name);
        Assert.Equal(SprintProjectEndpointTypes.Admin, endpoint.Type);
    }


    [Fact]
    public async Task FixBugAsync_KeepsRequirementPendingFixWhenOtherBugsRemainOpen()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-MULTI-BUG", "Multi bug path"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Fix multiple defects", null, 2),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        await service.ClaimRequirementAsync(requirement.Id, new ClaimSprintTaskRequest(null), "dev-1");
        requirement = await service.CompleteRequirementDevelopmentAsync(
            requirement.Id,
            new CompleteSprintDevelopmentRequest(null));
        requirement = await service.StartRequirementTestingAsync(requirement.Id);
        var firstBug = await service.CreateBugAsync(
            new CreateSprintBugRequest(
                project.Id,
                requirement.Id,
                "First defect",
                null,
                "test",
                "major",
                null,
                null),
            "tester-1");
        await service.CreateBugAsync(
            new CreateSprintBugRequest(
                project.Id,
                requirement.Id,
                "Second defect",
                null,
                "test",
                "major",
                null,
                null),
            "tester-1");

        await service.ClaimBugAsync(firstBug.Id, new ClaimSprintTaskRequest("devbox-1"), "dev-1");
        await service.FixBugAsync(firstBug.Id);

        var afterFix = (await service.ListRequirementsAsync(project.Id)).Single();
        Assert.Equal(SprintRequirementStatuses.PendingFix, afterFix.Status);
        Assert.Equal("warn", afterFix.Health);
    }

    [Fact]
    public async Task RequirementFeedback_CanConvertCompletedRequirementFeedbackIntoFollowUpDraft()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-FEEDBACK", "Feedback loop"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Accepted feature", "Original scope", 2),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        await service.ClaimRequirementAsync(requirement.Id, new ClaimSprintTaskRequest(null), "dev-1");
        requirement = await service.CompleteRequirementDevelopmentAsync(
            requirement.Id,
            new CompleteSprintDevelopmentRequest(null));
        requirement = await service.StartRequirementTestingAsync(requirement.Id);
        requirement = await service.MarkRequirementTestedAsync(requirement.Id);
        requirement = await service.CloseRequirementAsync(requirement.Id);

        var feedback = await service.CreateRequirementFeedbackAsync(
            requirement.Id,
            new CreateSprintRequirementFeedbackRequest(
                "Add export filter",
                "After acceptance, product wants a filter before exporting data."),
            "po-1");
        var followUp = await service.ConvertRequirementFeedbackAsync(
            requirement.Id,
            feedback.Id,
            new ConvertSprintRequirementFeedbackRequest(
                "Add export filter",
                null,
                1,
                "pm-1,arch-1"),
            "po-1");
        var feedbackList = await service.ListRequirementFeedbackAsync(requirement.Id);

        Assert.Equal(SprintRequirementFeedbackStatuses.Converted, feedbackList.Single().Status);
        Assert.Equal(followUp.Id, feedbackList.Single().ConvertedRequirementId);
        Assert.Equal(SprintRequirementStatuses.Draft, followUp.Status);
        Assert.Equal(requirement.Id, followUp.SourceRequirementId);
        Assert.Equal(feedback.Id, followUp.SourceFeedbackId);
        Assert.Equal("After acceptance, product wants a filter before exporting data.", followUp.Description);
    }

    [Fact]
    public async Task ConvertRequirementSourcesAsync_ConvertsTaskFeedbackAndSuggestionWithRemark()
    {
        var suggestionDomain = new InMemorySprintFeatureSuggestionDomain();
        var feedbackDomain = new InMemorySprintRequirementFeedbackDomain();
        var taskDomain = new InMemorySprintDevelopmentTaskDomain();
        var service = new AgileMvpService(
            new InMemorySprintProjectDomain(),
            new InMemorySprintProjectMemberDomain(),
            new InMemorySprintProjectEndpointDomain(),
            new InMemorySprintFeatureModuleDomain(),
            new InMemorySprintRequirementDomain(),
            new InMemorySprintSkillDomain(),
            suggestionDomain,
            feedbackDomain,
            new InMemorySprintRequirementReviewDomain(),
            taskDomain,
            new InMemorySprintBugDomain(),
            new InMemorySprintTaskLeaseDomain(),
            new InMemoryAgileRuntimeEnvironmentDomain(),
            new InMemoryGitRepositoryDomain(),
            new InMemoryGitAccountDomain(),
            new InMemoryAgilePromptTemplateDomain(),
            new InMemoryAgileTestPlanDomain(),
            new InMemoryAgileDigitalWorkerDomain(),
            new InMemoryAgileWorkerCommandDomain(),
            new RequirementDecompositionService(),
            new StaticSystemConfigurationService());
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-SOURCES", "Source conversion"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Accepted feature", "Original scope", 2),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null, SprintTaskAssignmentModes.Manual),
            "po-1");
        var assigned = await service.AssignDevelopmentTaskAsync(
            tasks[0].Id,
            new AssignSprintDevelopmentTaskRequest("dev-1"),
            "pm-1");
        await service.GetDevelopmentTaskPromptAsync(assigned.Id, "dev-1");
        await service.CompleteDevelopmentTaskAsync(assigned.Id, "dev-1");
        requirement = await service.StartRequirementTestingAsync(requirement.Id);
        requirement = await service.MarkRequirementTestedAsync(requirement.Id);

        var feedback = await service.CreateRequirementFeedbackAsync(
            requirement.Id,
            new CreateSprintRequirementFeedbackRequest(
                "Improve completed task",
                "The completed task needs an export switch.",
                assigned.Id),
            "po-1");
        var suggestion = await service.CreateFeatureSuggestionAsync(
            new CreateSprintFeatureSuggestionRequest(
                project.Id,
                requirement.EndpointId,
                requirement.ModuleId,
                requirement.Id,
                "Add a preset for export switches."),
            "pm-1");
        var followUp = await service.ConvertRequirementSourcesAsync(
            requirement.Id,
            new ConvertSprintRequirementSourcesRequest(
                "Export switch follow-up",
                "Implement export switch follow-up.",
                1,
                "pm-1,arch-1",
                [feedback.Id],
                [suggestion.Id],
                "Need beta toggle."),
            "po-1");
        var convertedFeedback = Assert.Single(await service.ListRequirementFeedbackAsync(requirement.Id));
        var convertedSuggestion = Assert.Single(await service.ListFeatureSuggestionsAsync(project.Id, requirement.ModuleId, requirement.Id));

        Assert.Equal(SprintRequirementStatuses.Draft, followUp.Status);
        Assert.Equal(requirement.Id, followUp.SourceRequirementId);
        Assert.Null(followUp.SourceFeedbackId);
        Assert.Equal($"Implement export switch follow-up.{Environment.NewLine}{Environment.NewLine}追加备注: Need beta toggle.", followUp.Description);
        Assert.Equal(assigned.Id, convertedFeedback.DevelopmentTaskId);
        Assert.Equal(SprintRequirementFeedbackStatuses.Converted, convertedFeedback.Status);
        Assert.Equal(followUp.Id, convertedFeedback.ConvertedRequirementId);
        Assert.Equal(SprintFeatureSuggestionStatuses.Accepted, convertedSuggestion.Status);
        Assert.Equal(followUp.Id, convertedSuggestion.ConvertedRequirementId);
        Assert.NotNull(convertedSuggestion.ConvertedAt);
    }

    [Fact]
    public async Task EndpointAndRequirement_CanSelectActiveSkills()
    {
        var service = CreateService();
        var skill = await service.CreateSkillAsync(
            new CreateSprintSkillRequest(
                "AIR-CLOUD",
                "Air.Cloud delivery gate",
                "Follow Air.Cloud coding, testing and documentation rules.",
                "Repository delivery gate",
                SprintSkillTypes.RequirementAnalysis),
            "admin-1");

        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-SKILL", "Skill selected project"),
            "pm-1");
        var endpoint = await service.CreateProjectEndpointAsync(
            new CreateSprintProjectEndpointRequest(
                project.Id,
                "ADMIN",
                "Admin console",
                SprintProjectEndpointTypes.Admin,
                SkillIds: [skill.Id]),
            "pm-1");
        var module = await service.CreateFeatureModuleAsync(
            new CreateSprintFeatureModuleRequest(
                project.Id,
                endpoint.Id,
                "GENERAL",
                "General",
                null),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(
                project.Id,
                "Build with skill",
                "Use selected skill",
                1,
                EndpointId: endpoint.Id,
                ModuleId: module.Id),
            "po-1");

        Assert.Equal([skill.Id], endpoint.SkillIds);
        Assert.Equal([skill.Id], requirement.SkillIds);
        var listed = Assert.Single(await service.ListSkillsAsync(activeOnly: true));
        Assert.Equal("AIR-CLOUD", listed.Code);
        Assert.Equal(SprintSkillTypes.RequirementAnalysis, listed.Type);
    }

    [Fact]
    public async Task CreateSkillAsync_GeneratesCodeWhenCodeIsMissing()
    {
        var service = CreateService();

        var skill = await service.CreateSkillAsync(
            new CreateSprintSkillRequest(null, "Imported skill", "Use imported markdown content."),
            "admin-1");

        Assert.StartsWith("SKILL-", skill.Code);
        Assert.Equal(14, skill.Code.Length);
        Assert.Equal("Imported skill", skill.Name);
    }

    [Fact]
    public async Task UpdateSkillAsync_CanChangeType()
    {
        var service = CreateService();
        var skill = await service.CreateSkillAsync(
            new CreateSprintSkillRequest("DEBUG", "Debug skill", "Debug runtime problems"),
            "admin-1");

        var updated = await service.UpdateSkillAsync(
            skill.Id,
            new UpdateSprintSkillRequest(
                skill.Name,
                skill.Content,
                skill.Description,
                SprintSkillStatuses.Active,
                SprintSkillTypes.Debugging));

        Assert.Equal(SprintSkillTypes.Development, skill.Type);
        Assert.Equal(SprintSkillTypes.Debugging, updated.Type);
    }

    [Fact]
    public async Task CreateRequirementAsync_RejectsDisabledSkill()
    {
        var service = CreateService();
        var skill = await service.CreateSkillAsync(
            new CreateSprintSkillRequest("DISABLED", "Disabled skill", "Do not select"),
            "admin-1");
        await service.UpdateSkillAsync(
            skill.Id,
            new UpdateSprintSkillRequest(skill.Name, skill.Content, skill.Description, SprintSkillStatuses.Disabled));
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DISABLED-SKILL", "Disabled skill guard"),
            "pm-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequirementAsync(
                new CreateSprintRequirementRequest(
                    project.Id,
                    "Cannot select disabled skill",
                    null,
                    2,
                    SkillIds: [skill.Id]),
                "po-1"));

        Assert.Equal("Selected skill does not exist or is disabled.", exception.Message);
    }

    [Fact]
    public async Task ConvertRequirementFeedbackAsync_RejectsAlreadyConvertedFeedback()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-FEEDBACK-GUARD", "Feedback guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Accepted feature", null, 2),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        await service.ClaimRequirementAsync(requirement.Id, new ClaimSprintTaskRequest(null), "dev-1");
        requirement = await service.CompleteRequirementDevelopmentAsync(
            requirement.Id,
            new CompleteSprintDevelopmentRequest(null));
        requirement = await service.StartRequirementTestingAsync(requirement.Id);
        requirement = await service.MarkRequirementTestedAsync(requirement.Id);
        var feedback = await service.CreateRequirementFeedbackAsync(
            requirement.Id,
            new CreateSprintRequirementFeedbackRequest("Follow-up", "Create once."),
            "po-1");
        await service.ConvertRequirementFeedbackAsync(
            requirement.Id,
            feedback.Id,
            new ConvertSprintRequirementFeedbackRequest("Follow-up", null, null, null),
            "po-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConvertRequirementFeedbackAsync(
                requirement.Id,
                feedback.Id,
                new ConvertSprintRequirementFeedbackRequest("Follow-up again", null, null, null),
                "po-1"));

        Assert.Equal("Feedback status does not allow conversion.", exception.Message);
    }

    [Fact]
    public async Task CreateRequirementFeedbackAsync_RejectsCompletedFollowUpRequirement()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-FEEDBACK-FOLLOWUP-GUARD", "Feedback follow-up guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Accepted feature", null, 2),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        await service.ClaimRequirementAsync(requirement.Id, new ClaimSprintTaskRequest(null), "dev-1");
        requirement = await service.CompleteRequirementDevelopmentAsync(
            requirement.Id,
            new CompleteSprintDevelopmentRequest(null));
        requirement = await service.StartRequirementTestingAsync(requirement.Id);
        requirement = await service.MarkRequirementTestedAsync(requirement.Id);
        requirement = await service.CloseRequirementAsync(requirement.Id);
        var feedback = await service.CreateRequirementFeedbackAsync(
            requirement.Id,
            new CreateSprintRequirementFeedbackRequest("Follow-up", "Create follow-up requirement."),
            "po-1");
        var followUp = await service.ConvertRequirementFeedbackAsync(
            requirement.Id,
            feedback.Id,
            new ConvertSprintRequirementFeedbackRequest("Follow-up", null, null, null),
            "po-1");

        followUp = await SubmitAndApproveRequirementAsync(service, followUp.Id, "pm-1");
        await service.ClaimRequirementAsync(followUp.Id, new ClaimSprintTaskRequest(null), "dev-1");
        followUp = await service.CompleteRequirementDevelopmentAsync(
            followUp.Id,
            new CompleteSprintDevelopmentRequest(null));
        followUp = await service.StartRequirementTestingAsync(followUp.Id);
        followUp = await service.MarkRequirementTestedAsync(followUp.Id);
        followUp = await service.CloseRequirementAsync(followUp.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequirementFeedbackAsync(
                followUp.Id,
                new CreateSprintRequirementFeedbackRequest("Nested feedback", "Should be rejected."),
                "po-1"));

        Assert.Equal(
            "Follow-up requirement converted from feedback cannot create feedback again.",
            exception.Message);
    }

    [Fact]
    public async Task UpdateProject_ChangesEditableConfigurationWithoutChangingCode()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-UPDATE", "Original"),
            "pm-1");

        var updated = await service.UpdateProjectAsync(
            project.Id,
            new UpdateSprintProjectRequest(
                "Updated",
                " http://localhost:5999 ",
                " Updated detail ",
                " Vue 3 ",
                " .NET 10 ",
                "manager-2",
                ["pm-2", "pm-1"],
                ["dev-2"],
                ["tester-2"],
                "arch-2"));

        Assert.Equal("MVP-UPDATE", updated.Code);
        Assert.Equal("Updated", updated.Name);
        Assert.Equal("http://localhost:5999", updated.TestEnvironmentUrl);
        Assert.Equal("Updated detail", updated.Description);
        Assert.Equal("Vue 3", updated.FrontendTechStack);
        Assert.Equal(".NET 10", updated.BackendTechStack);
        Assert.Equal("manager-2", updated.ProjectManagerId);
        Assert.Equal(["pm-2", "pm-1"], updated.ProductManagerIds);
        Assert.Equal(["dev-2"], updated.DeveloperIds);
        Assert.Equal(["tester-2"], updated.TesterIds);
        Assert.Equal("arch-2", updated.ArchitectId);
    }

    [Fact]
    public async Task UpdateProject_RejectsEmptyName()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-EMPTY", "Original"),
            "pm-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateProjectAsync(project.Id, new UpdateSprintProjectRequest("", null)));

        Assert.Equal("Project name is required.", exception.Message);
    }

    [Fact]
    public async Task CreateProject_UsesSelectedGitRepositoryAndAccount()
    {
        var repositoryDomain = new InMemoryGitRepositoryDomain();
        var accountDomain = new InMemoryGitAccountDomain();
        var account = new GitAccountEntity
        {
            Code = "MAIN",
            Name = "Main account",
            Username = "codex",
            AccessToken = "token",
            Status = GitAccountStatuses.Active,
            CreatedBy = "admin"
        };
        var repository = new GitRepositoryEntity
        {
            Code = "AGENTSPRINT",
            Name = "AgentSprint",
            RepositoryUrl = "https://example.com/selected.git",
            DefaultBranch = "main",
            GitAccountId = account.Id,
            Status = GitRepositoryStatuses.Active,
            CreatedBy = "admin"
        };
        await accountDomain.CreateAsync(account);
        await repositoryDomain.CreateAsync(repository);
        var service = CreateService(
            gitRepositoryDomain: repositoryDomain,
            gitAccountDomain: accountDomain);

        var project = await service.CreateProjectAsync(
            CreateProjectRequest(
                "MVP-GIT-SOURCE",
                "Git source",
                gitRepositoryId: repository.Id),
            "pm-1");

        Assert.Equal(repository.Id, project.GitRepositoryId);
        Assert.Equal(account.Id, project.GitAccountId);
    }

    [Fact]
    public async Task CreateProject_AllowsProjectWithoutGitSelection()
    {
        var service = CreateService();

        var project = await service.CreateProjectAsync(
                CreateProjectRequest("MVP-NO-REPO", "No repo"),
                "pm-1");

        Assert.Null(project.GitRepositoryId);
        Assert.Null(project.GitAccountId);
    }

    [Fact]
    public async Task CreateProject_SyncsConfiguredTeamMembers()
    {
        var projectMemberDomain = new InMemorySprintProjectMemberDomain();
        var service = CreateService(projectMemberDomain: projectMemberDomain);

        var project = await service.CreateProjectAsync(
            CreateProjectRequest(
                "MVP-PROFILE",
                "Project profile",
                projectManagerId: "manager-1",
                productManagerIds: ["pm-1", "pm-2"],
                developerIds: ["dev-1", "dev-2"],
                architectId: "arch-1"),
            "creator-1");
        var members = await projectMemberDomain.ListAsync(entity => entity.ProjectId == project.Id);

        Assert.Equal("manager-1", project.ProjectManagerId);
        Assert.Equal(["pm-1", "pm-2"], project.ProductManagerIds);
        Assert.Equal(["dev-1", "dev-2"], project.DeveloperIds);
        Assert.Equal("arch-1", project.ArchitectId);
        Assert.Contains(members, member => member.UserId == "manager-1" && member.Role == SprintProjectMemberRoles.ProjectManager);
        Assert.Contains(members, member => member.UserId == "pm-1" && member.Role == SprintProjectMemberRoles.Product);
        Assert.Contains(members, member => member.UserId == "pm-2" && member.Role == SprintProjectMemberRoles.Product);
        Assert.Contains(members, member => member.UserId == "dev-1" && member.Role == SprintProjectMemberRoles.Developer);
        Assert.Contains(members, member => member.UserId == "dev-2" && member.Role == SprintProjectMemberRoles.Developer);
        Assert.Contains(members, member => member.UserId == "arch-1" && member.Role == SprintProjectMemberRoles.Architect);
    }

    [Fact]
    public async Task CreateProject_DoesNotDuplicateCreatorWhenCreatorIsProjectManager()
    {
        var projectMemberDomain = new InMemorySprintProjectMemberDomain();
        var service = CreateService(projectMemberDomain: projectMemberDomain);

        var project = await service.CreateProjectAsync(
            CreateProjectRequest(
                "MVP-CREATOR-MANAGER",
                "Creator manager",
                projectManagerId: "manager-1",
                developerIds: ["manager-1"],
                architectId: "manager-1"),
            "manager-1");
        var members = await projectMemberDomain.ListAsync(entity => entity.ProjectId == project.Id);

        Assert.Single(members, member =>
            member.UserId == "manager-1" &&
            member.Role == SprintProjectMemberRoles.ProjectManager);
        Assert.Contains(members, member =>
            member.UserId == "manager-1" &&
            member.Role == SprintProjectMemberRoles.Developer);
        Assert.Contains(members, member =>
            member.UserId == "manager-1" &&
            member.Role == SprintProjectMemberRoles.Architect);
    }

    [Fact]
    public async Task ListDevelopmentTasks_FiltersByStatus()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-TASK-STATUS", "Task status"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Split tasks", "Build task hall", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");

        var assignedTasks = await service.ListDevelopmentTasksAsync(
            project.Id,
            requirement.Id,
            null,
            null,
            SprintDevelopmentTaskStatuses.Assigned);

        Assert.NotEmpty(assignedTasks);
        Assert.All(assignedTasks, task => Assert.Equal(SprintDevelopmentTaskStatuses.Assigned, task.Status));
    }

    [Fact]
    public async Task ListDevelopmentTasks_FiltersByRelatedUserAsAssigneeOrAssigner()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-TASK-RELATED-USER", "Task related user"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Related user filter", "Filter by assignee or assigner", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null, SprintTaskAssignmentModes.Manual),
            "po-1");
        var task = Assert.Single(tasks);
        await service.AssignDevelopmentTaskAsync(
            task.Id,
            new AssignSprintDevelopmentTaskRequest("dev-related"),
            "pm-related");

        var assigneeTasks = await service.ListDevelopmentTasksAsync(project.Id, requirement.Id, null, "dev-related");
        var assignerTasks = await service.ListDevelopmentTasksAsync(project.Id, requirement.Id, null, "pm-related");
        var unrelatedTasks = await service.ListDevelopmentTasksAsync(project.Id, requirement.Id, null, "user-other");

        Assert.Single(assigneeTasks, item => item.Id == task.Id);
        Assert.Single(assignerTasks, item => item.Id == task.Id);
        Assert.DoesNotContain(unrelatedTasks, item => item.Id == task.Id);
    }

    [Fact]
    public async Task ApproveRequirementReviewAsync_DoesNotCreateTasksBeforeManualDecomposition()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-MANUAL-TASK", "Manual task hall"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Manual create tasks", "Tasks should wait for decomposition", 1),
            "po-1");
        await service.SubmitRequirementReviewAsync(
            requirement.Id,
            new SubmitSprintRequirementReviewRequest(["pm-1", "arch-1"]),
            "po-1");
        await service.ApproveRequirementReviewAsync(
            requirement.Id,
            "pm-1",
            new DecideSprintRequirementReviewRequest("ok"));

        var reviewed = await service.ApproveRequirementReviewAsync(
            requirement.Id,
            "arch-1",
            new DecideSprintRequirementReviewRequest("ok"));
        var tasksBeforeDecomposition = await service.ListParticipatingDevelopmentTasksAsync(
            project.Id,
            requirement.Id,
            null,
            null,
            SprintDevelopmentTaskStatuses.PendingAssign,
            false,
            "pm-1");

        Assert.Equal(SprintRequirementStatuses.Approved, reviewed.Status);
        Assert.Empty(tasksBeforeDecomposition);

        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null, SprintTaskAssignmentModes.Manual),
            "po-1");

        Assert.NotEmpty(tasks);
        Assert.All(tasks, task =>
        {
            Assert.Equal(project.Id, task.ProjectId);
            Assert.Equal(requirement.Id, task.RequirementId);
            Assert.Equal(SprintDevelopmentTaskStatuses.PendingAssign, task.Status);
        });
    }

    [Fact]
    public async Task DecomposeRequirementAsync_ManualModeAssignsSelectedDeveloper()
    {
        var projectMemberDomain = new InMemorySprintProjectMemberDomain();
        var service = CreateService(projectMemberDomain: projectMemberDomain);
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-MANUAL-ASSIGNEE", "Manual assignee"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Manual assigned tasks", "Assign on decomposition", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");

        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(
                "Create implementation task.",
                SprintTaskAssignmentModes.Manual,
                2,
                "dev-selected"),
            "po-1");
        var updatedRequirement = (await service.ListRequirementsAsync(project.Id)).Single();
        var projectMembers = await projectMemberDomain.ListAsync(entity =>
            entity.ProjectId == project.Id &&
            entity.UserId == "dev-selected" &&
            entity.Role == SprintProjectMemberRoles.Developer);

        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, task =>
        {
            Assert.Equal("dev-selected", task.AssigneeId);
            Assert.Equal(SprintTaskAssigneeTypes.Employee, task.AssigneeType);
            Assert.Equal("po-1", task.AssignedBy);
            Assert.Equal(SprintDevelopmentTaskStatuses.Assigned, task.Status);
            Assert.NotNull(task.AssignedAt);
            Assert.False(string.IsNullOrWhiteSpace(task.Prompt));
        });
        Assert.Equal(SprintRequirementStatuses.Decomposed, updatedRequirement.Status);
        Assert.Equal("dev-selected", updatedRequirement.DeveloperId);
        Assert.Single(projectMembers);
    }

    [Fact]
    public async Task DecomposeRequirementAsync_ManualModePreservesDigitalWorkerAssigneeType()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DIGITAL-DECOMPOSE", "Digital decompose"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Digital assigned tasks", "Assign worker", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");

        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(
                "Create implementation task.",
                SprintTaskAssignmentModes.Manual,
                1,
                "worker-agent-1",
                SprintTaskAssigneeTypes.DigitalWorker),
            "po-1");

        var task = Assert.Single(tasks);
        Assert.Equal("worker-agent-1", task.AssigneeId);
        Assert.Equal(SprintTaskAssigneeTypes.DigitalWorker, task.AssigneeType);
    }

    [Fact]
    public async Task DecomposeRequirementAsync_DefaultsToSingleTaskWhenTaskCountIsNotConfigured()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DECOMPOSE-DEFAULT-COUNT", "Default task count"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Default one task", "Do not split too much by default", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");

        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null, SprintTaskAssignmentModes.Manual),
            "po-1");

        var task = Assert.Single(tasks);
        Assert.Equal("完成需求交付 - Default one task", task.Title);
        Assert.Equal(SprintDevelopmentTaskStatuses.PendingAssign, task.Status);
    }

    [Fact]
    public async Task DecomposeRequirementAsync_UsesConfiguredTaskCount()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DECOMPOSE-CONFIG-COUNT", "Configured task count"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Configured three tasks", "Split only when configured", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");

        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null, SprintTaskAssignmentModes.Manual, 3),
            "po-1");

        Assert.Equal(3, tasks.Count);
        Assert.Contains(tasks, task => task.Title == "梳理实现方案 - Configured three tasks");
        Assert.Contains(tasks, task => task.Title == "实现业务闭环 - Configured three tasks");
        Assert.Contains(tasks, task => task.Title == "补充验证用例 - Configured three tasks");
    }

    [Fact]
    public async Task ApproveRequirementReviewAsync_AdvancesWhenReviewQueryReturnsPersistedSnapshot()
    {
        var reviewDomain = new SnapshotOnListSprintRequirementReviewDomain();
        var taskDomain = new InMemorySprintDevelopmentTaskDomain();
        var service = new AgileMvpService(
            new InMemorySprintProjectDomain(),
            new InMemorySprintProjectMemberDomain(),
            new InMemorySprintProjectEndpointDomain(),
            new InMemorySprintFeatureModuleDomain(),
            new InMemorySprintRequirementDomain(),
            new InMemorySprintSkillDomain(),
            new InMemorySprintFeatureSuggestionDomain(),
            new InMemorySprintRequirementFeedbackDomain(),
            reviewDomain,
            taskDomain,
            new InMemorySprintBugDomain(),
            new InMemorySprintTaskLeaseDomain(),
            new InMemoryAgileRuntimeEnvironmentDomain(),
            new InMemoryGitRepositoryDomain(),
            new InMemoryGitAccountDomain(),
            new InMemoryAgilePromptTemplateDomain(),
            new InMemoryAgileTestPlanDomain(),
            new InMemoryAgileDigitalWorkerDomain(),
            new InMemoryAgileWorkerCommandDomain(),
            new RequirementDecompositionService(),
            new StaticSystemConfigurationService());
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-REVIEW-SNAPSHOT", "Review snapshot"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Single reviewer snapshot", "Approve once", 1),
            "pm-1");
        await service.SubmitRequirementReviewAsync(
            requirement.Id,
            new SubmitSprintRequirementReviewRequest(["pm-1"]),
            "pm-1");

        var reviewed = await service.ApproveRequirementReviewAsync(
            requirement.Id,
            "pm-1",
            new DecideSprintRequirementReviewRequest("ok"));
        var tasks = await service.ListDevelopmentTasksAsync(project.Id, requirement.Id, null);

        Assert.Equal(SprintRequirementStatuses.Approved, reviewed.Status);
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task ListDevelopmentTasksAsync_DoesNotRepairApprovedReviewIntoGeneratedTasks()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var reviewDomain = new InMemorySprintRequirementReviewDomain();
        var taskDomain = new InMemorySprintDevelopmentTaskDomain();
        var service = new AgileMvpService(
            new InMemorySprintProjectDomain(),
            new InMemorySprintProjectMemberDomain(),
            new InMemorySprintProjectEndpointDomain(),
            new InMemorySprintFeatureModuleDomain(),
            requirementDomain,
            new InMemorySprintSkillDomain(),
            new InMemorySprintFeatureSuggestionDomain(),
            new InMemorySprintRequirementFeedbackDomain(),
            reviewDomain,
            taskDomain,
            new InMemorySprintBugDomain(),
            new InMemorySprintTaskLeaseDomain(),
            new InMemoryAgileRuntimeEnvironmentDomain(),
            new InMemoryGitRepositoryDomain(),
            new InMemoryGitAccountDomain(),
            new InMemoryAgilePromptTemplateDomain(),
            new InMemoryAgileTestPlanDomain(),
            new InMemoryAgileDigitalWorkerDomain(),
            new InMemoryAgileWorkerCommandDomain(),
            new RequirementDecompositionService(),
            new StaticSystemConfigurationService());
        var requirement = new SprintRequirementEntity
        {
            ProjectId = "project-1",
            Title = "Recovered approved requirement",
            Description = "Review was approved before task generation completed.",
            Status = SprintRequirementStatuses.PendingReview,
            CreatedBy = "po-1"
        };
        await requirementDomain.CreateAsync(requirement);
        await reviewDomain.CreateAsync(new SprintRequirementReviewEntity
        {
            ProjectId = requirement.ProjectId,
            RequirementId = requirement.Id,
            ReviewerId = "arch-1",
            Status = SprintRequirementReviewStatuses.Approved,
            ReviewedAt = DateTime.UtcNow
        });

        var tasks = await service.ListDevelopmentTasksAsync(
            requirement.ProjectId,
            requirement.Id,
            null,
            null,
            SprintDevelopmentTaskStatuses.PendingAssign);
        var repaired = await requirementDomain.GetAsync(requirement.Id);

        Assert.NotNull(repaired);
        Assert.Equal(SprintRequirementStatuses.PendingReview, repaired.Status);
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task ListParticipatingDevelopmentTasksAsync_RestrictsManagerToJoinedProjects()
    {
        var service = CreateService();
        var firstProject = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-PARTICIPATING-1", "Participating first"),
            "pm-1");
        var firstRequirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(firstProject.Id, "Visible task", "Visible to pm-1", 1),
            "po-1");
        firstRequirement = await SubmitAndApproveRequirementAsync(service, firstRequirement.Id, "pm-1");
        await service.DecomposeRequirementAsync(
            firstRequirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");

        var secondProject = await service.CreateProjectAsync(
            CreateProjectRequest(
                "MVP-PARTICIPATING-2",
                "Participating second",
                projectManagerId: "manager-2",
                productManagerIds: ["pm-2"],
                developerIds: ["dev-2"],
                architectId: "arch-2"),
            "pm-2");
        var secondRequirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(secondProject.Id, "Hidden task", "Hidden from pm-1", 1),
            "po-2");
        secondRequirement = await SubmitAndApproveRequirementAsync(service, secondRequirement.Id, "pm-2", "po-2");
        await service.DecomposeRequirementAsync(
            secondRequirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-2");

        var tasks = await service.ListParticipatingDevelopmentTasksAsync(null, null, null, null, null, false, "pm-1");

        Assert.NotEmpty(tasks);
        Assert.All(tasks, task =>
        {
            Assert.Equal(firstProject.Id, task.ProjectId);
            Assert.Equal(firstRequirement.Id, task.RequirementId);
        });
        Assert.DoesNotContain(tasks, task => task.ProjectId == secondProject.Id);
    }

    [Fact]
    public async Task ListParticipatingDevelopmentTasksAsync_PrimaryOnlyUsesModuleDevelopers()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest(
                "MVP-MODULE-PRIMARY",
                "Module primary",
                developerIds: ["project-dev"]),
            "pm-1");
        var endpoint = await service.CreateProjectEndpointAsync(
            new CreateSprintProjectEndpointRequest(
                project.Id,
                "WEB",
                "Web",
                "web",
                DeveloperIds: ["endpoint-dev"]),
            "pm-1");
        var module = await service.CreateFeatureModuleAsync(
            new CreateSprintFeatureModuleRequest(
                project.Id,
                endpoint.Id,
                "ORDER",
                "Order",
                null,
                DeveloperIds: ["module-dev"]),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(
                project.Id,
                "Module owned task",
                "Only module developer should see this in primary mode.",
                1,
                EndpointId: endpoint.Id,
                ModuleId: module.Id),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null, SprintTaskAssignmentModes.Manual),
            "po-1");

        var moduleDeveloperTasks = await service.ListParticipatingDevelopmentTasksAsync(
            project.Id,
            null,
            null,
            null,
            SprintDevelopmentTaskStatuses.PendingAssign,
            true,
            "module-dev");
        var endpointDeveloperTasks = await service.ListParticipatingDevelopmentTasksAsync(
            project.Id,
            null,
            null,
            null,
            SprintDevelopmentTaskStatuses.PendingAssign,
            true,
            "endpoint-dev");

        Assert.NotEmpty(moduleDeveloperTasks);
        Assert.All(moduleDeveloperTasks, task =>
        {
            Assert.Equal(endpoint.Id, task.EndpointId);
            Assert.Equal(module.Id, task.ModuleId);
        });
        Assert.Empty(endpointDeveloperTasks);
    }

    [Fact]
    public async Task ClaimRequirement_RejectsSecondActiveLeaseForSameUserInProject()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-LEASE", "MVP lease path"),
            "pm-1");
        var first = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "First", null, 1),
            "po-1");
        var second = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Second", null, 2),
            "po-1");
        first = await SubmitAndApproveRequirementAsync(service, first.Id, "pm-1");
        second = await SubmitAndApproveRequirementAsync(service, second.Id, "pm-1");

        await service.ClaimRequirementAsync(first.Id, new ClaimSprintTaskRequest(null), "dev-1");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ClaimRequirementAsync(second.Id, new ClaimSprintTaskRequest(null), "dev-1"));

        Assert.Equal("User already has an active lease in this project.", exception.Message);
    }

    [Fact]
    public async Task ReviewAndDecompose_CreatesAssignableTasksAndPrompt()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest(
                "MVP-REVIEW",
                "MVP review path"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Build task hall", "Show decomposed tasks", 1, "pm-1"),
            "po-1");

        requirement = await service.SubmitRequirementReviewAsync(
            requirement.Id,
            new SubmitSprintRequirementReviewRequest(["pm-1", "arch-1"]),
            "po-1");
        await service.ApproveRequirementReviewAsync(
            requirement.Id,
            "pm-1",
            new DecideSprintRequirementReviewRequest("ok"));
        requirement = await service.ApproveRequirementReviewAsync(
            requirement.Id,
            "arch-1",
            new DecideSprintRequirementReviewRequest("ok"));
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest("Keep MVP scope."),
            "po-1");
        var assigned = await service.AssignDevelopmentTaskAsync(
            tasks[0].Id,
            new AssignSprintDevelopmentTaskRequest("dev-1"),
            "pm-1");
        var requirementBeforePrompt = (await service.ListRequirementsAsync(project.Id)).Single(item => item.Id == requirement.Id);
        var prompt = await service.GetDevelopmentTaskPromptAsync(assigned.Id, "dev-1");
        var requirementAfterPrompt = (await service.ListRequirementsAsync(project.Id)).Single(item => item.Id == requirement.Id);

        Assert.Equal(SprintRequirementStatuses.Approved, requirement.Status);
        Assert.Equal(SprintRequirementStatuses.Decomposed, requirementBeforePrompt.Status);
        Assert.Equal(SprintRequirementStatuses.Decomposed, requirementAfterPrompt.Status);
        Assert.NotEmpty(tasks);
        Assert.Equal("dev-1", assigned.AssigneeId);
        Assert.Equal(SprintTaskAssigneeTypes.Employee, assigned.AssigneeType);
        Assert.Equal("pm-1", assigned.AssignedBy);
        Assert.Contains(project.Code, prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains(assigned.Id, prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains($"Task {project.Code} {assigned.Id}", prompt.TaskExecutionPrompt.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Build task hall", prompt.TaskExecutionPrompt.Content, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(assigned.Description))
        {
            Assert.DoesNotContain(assigned.Description, prompt.TaskExecutionPrompt.Content, StringComparison.Ordinal);
        }
        Assert.Contains("/mcp", prompt.McpSetupPrompt.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Aa123456!", prompt.Prompt, StringComparison.Ordinal);
        Assert.Equal("MCP 接入配置", prompt.McpSetupPrompt.Title);
        Assert.Contains($"MCP {project.Code} {assigned.Id}", prompt.McpSetupPrompt.Content, StringComparison.Ordinal);
        Assert.Contains("Authorization", prompt.McpSetupPrompt.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("{{agentToken}}", prompt.McpSetupPrompt.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("{{projectCode}}", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("{{taskId}}", prompt.Prompt, StringComparison.Ordinal);
        Assert.NotEmpty(prompt.McpSetupPrompt.Usage);
        Assert.Equal("任务推进提示词", prompt.TaskExecutionPrompt.Title);
        Assert.Contains("stop-after-complete", prompt.TaskExecutionPrompt.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("按仓库内 Air.Cloud 风格完成实现、测试和文档同步", prompt.TaskExecutionPrompt.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("按仓库内 Air.Cloud 风格完成实现、测试和文档同步", prompt.Prompt, StringComparison.Ordinal);
        Assert.NotEmpty(prompt.TaskExecutionPrompt.Usage);
    }

    [Fact]
    public async Task AssignDevelopmentTaskAsync_PreservesDigitalWorkerAssigneeType()
    {
        var workerDomain = new InMemoryAgileDigitalWorkerDomain();
        var service = CreateService(digitalWorkerDomain: workerDomain);
        await workerDomain.CreateAsync(new DigitalWorkerEntity
        {
            Name = "Codex Worker",
            Code = "codex-worker",
            AgentUserId = "worker-agent-1",
            Status = DigitalWorkerStatuses.Active
        });
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DIGITAL-ASSIGN", "Digital assign"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Digital task", "Assign worker", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest("Keep MVP scope."),
            "po-1");

        var assigned = await service.AssignDevelopmentTaskAsync(
            tasks[0].Id,
            new AssignSprintDevelopmentTaskRequest(
                "worker-agent-1",
                SprintTaskAssigneeTypes.DigitalWorker),
            "pm-1");

        Assert.Equal("worker-agent-1", assigned.AssigneeId);
        Assert.Equal(SprintTaskAssigneeTypes.DigitalWorker, assigned.AssigneeType);
    }

    [Fact]
    public async Task AssignDevelopmentTaskAsync_CreatesStartTaskCommandForDigitalWorker()
    {
        var workerDomain = new InMemoryAgileDigitalWorkerDomain();
        var commandDomain = new InMemoryAgileWorkerCommandDomain();
        var service = CreateService(
            digitalWorkerDomain: workerDomain,
            workerCommandDomain: commandDomain);
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DW-CMD", "Digital worker command"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Run through worker", null, 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var task = (await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null, "agent-1", SprintTaskAssigneeTypes.DigitalWorker),
            "pm-1")).Single();
        var worker = new DigitalWorkerEntity
        {
            Name = "Codex Worker",
            Code = "codex-worker",
            AgentUserId = "agent-1",
            Status = DigitalWorkerStatuses.Active
        };
        await workerDomain.CreateAsync(worker);

        await service.AssignDevelopmentTaskAsync(
            task.Id,
            new AssignSprintDevelopmentTaskRequest("agent-1", SprintTaskAssigneeTypes.DigitalWorker),
            "pm-1");

        var command = Assert.Single(await commandDomain.ListAsync());
        Assert.Equal(worker.Id, command.WorkerId);
        Assert.Equal(WorkerCommandTypes.StartTask, command.CommandType);
        Assert.Equal(WorkerCommandStatuses.Pending, command.Status);
        Assert.Contains(task.Id, command.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssignDevelopmentTaskAsync_RejectsTaskThatHasActiveLease()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-ASSIGN-GUARD", "Assign guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Prevent double assign", null, 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");
        var assigned = await service.AssignDevelopmentTaskAsync(
            tasks[0].Id,
            new AssignSprintDevelopmentTaskRequest("dev-1"),
            "pm-1");
        await service.ClaimDevelopmentTaskAsync(
            assigned.Id,
            new ClaimSprintTaskRequest("window-1"),
            "dev-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignDevelopmentTaskAsync(
                assigned.Id,
                new AssignSprintDevelopmentTaskRequest("dev-2"),
                "pm-2"));

        Assert.Equal("Target already has an active lease.", exception.Message);
    }

    [Fact]
    public async Task ClaimDevelopmentTaskAsync_CreatesLeaseAndRejectsDifferentWindowForSameTask()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-TASK-LEASE", "Task lease guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Lease task", null, 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");
        var assigned = await service.AssignDevelopmentTaskAsync(
            tasks[0].Id,
            new AssignSprintDevelopmentTaskRequest("dev-1"),
            "pm-1");
        var beforeClaimRequirement = (await service.ListRequirementsAsync(project.Id)).Single();

        var lease = await service.ClaimDevelopmentTaskAsync(
            assigned.Id,
            new ClaimSprintTaskRequest("window-1"),
            "dev-1");
        var sameWindowLease = await service.ClaimDevelopmentTaskAsync(
            assigned.Id,
            new ClaimSprintTaskRequest("window-1"),
            "dev-1");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ClaimDevelopmentTaskAsync(
                assigned.Id,
                new ClaimSprintTaskRequest("window-2"),
                "dev-1"));
        var claimedRequirement = (await service.ListRequirementsAsync(project.Id)).Single();

        Assert.Equal(SprintRequirementStatuses.Decomposed, beforeClaimRequirement.Status);
        Assert.Equal(SprintRequirementStatuses.Decomposed, claimedRequirement.Status);
        Assert.Equal(SprintTaskTargetTypes.DevelopmentTask, lease.TargetType);
        Assert.Equal(assigned.Id, lease.TargetId);
        Assert.Equal(lease.Id, sameWindowLease.Id);
        Assert.Equal("Target already has an active lease.", exception.Message);
    }

    [Fact]
    public async Task GetDevelopmentTaskPromptAsync_DoesNotStartAssignedTask()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-PROMPT-NO-START", "Prompt should not start task"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Keep assigned until worker starts", null, 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");
        var assigned = await service.AssignDevelopmentTaskAsync(
            tasks[0].Id,
            new AssignSprintDevelopmentTaskRequest("dev-1"),
            "pm-1");

        await service.GetDevelopmentTaskPromptAsync(assigned.Id, "dev-1");
        var afterPromptTask = (await service.ListDevelopmentTasksAsync(project.Id, requirement.Id, null)).Single();
        var afterPromptRequirement = (await service.ListRequirementsAsync(project.Id)).Single();

        Assert.Equal(SprintDevelopmentTaskStatuses.Assigned, afterPromptTask.Status);
        Assert.Null(afterPromptTask.StartedAt);
        Assert.Equal(SprintRequirementStatuses.Decomposed, afterPromptRequirement.Status);
    }

    [Fact]
    public async Task GetDevelopmentTaskPromptAsync_UsesConfiguredMcpEndpoint()
    {
        var service = CreateService(configurationService: new StaticSystemConfigurationService(
            "Mcp:Endpoint",
            "http://agentsprint.example.com/mcp"));
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-MCP-CONFIG", "MCP endpoint config"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Use configured MCP", "Prompt should use config table value", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");
        var assigned = await service.AssignDevelopmentTaskAsync(
            tasks[0].Id,
            new AssignSprintDevelopmentTaskRequest("dev-1"),
            "pm-1");

        var prompt = await service.GetDevelopmentTaskPromptAsync(assigned.Id, "dev-1");

        Assert.Contains("http://agentsprint.example.com/mcp", prompt.McpSetupPrompt.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("http://localhost:5010/mcp", prompt.McpSetupPrompt.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDevelopmentTaskPromptAsync_RedactsRepositoryCredentials()
    {
        var repositoryDomain = new InMemoryGitRepositoryDomain();
        var repository = new GitRepositoryEntity
        {
            Code = "SECRET",
            Name = "Secret Repository",
            RepositoryUrl = "https://token-user:s3cr3t@example.com/org/repo.git",
            DefaultBranch = "main",
            Status = GitRepositoryStatuses.Active,
            CreatedBy = "admin"
        };
        await repositoryDomain.CreateAsync(repository);
        var service = CreateService(gitRepositoryDomain: repositoryDomain);
        var project = await service.CreateProjectAsync(
            CreateProjectRequest(
                "MVP-PROMPT-SECRET",
                "Prompt secret guard",
                gitRepositoryId: repository.Id),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Protect task prompt", "Do not leak credentials", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");
        var assigned = await service.AssignDevelopmentTaskAsync(
            tasks[0].Id,
            new AssignSprintDevelopmentTaskRequest("dev-1"),
            "pm-1");

        var prompt = await service.GetDevelopmentTaskPromptAsync(assigned.Id, "dev-1");

        Assert.Contains("https://example.com/org/repo.git", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("token-user", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("Authorization", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("http_headers = { Authorization = \"Bearer <这里填令牌>\" }", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(Environment.NewLine + "[mcp_servers.agentsprint.headers]" + Environment.NewLine, prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("\"X-AgentSprint-Api-Base-Url\" = \"http://localhost:5000\"", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("X-AgentSprint-Password", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("X-AgentSprint-Workspace-Path", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("token-user", prompt.TaskExecutionPrompt.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", prompt.TaskExecutionPrompt.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDevelopmentTaskPromptAsync_RejectsUserWhoIsNotAssignee()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-PROMPT-OWNER", "Prompt owner guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Protect prompt ownership", "Only assignee can advance", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");
        var assigned = await service.AssignDevelopmentTaskAsync(
            tasks[0].Id,
            new AssignSprintDevelopmentTaskRequest("dev-1"),
            "pm-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetDevelopmentTaskPromptAsync(assigned.Id, "dev-2"));

        Assert.Equal("Task is not assigned to current user.", exception.Message);
    }

    [Fact]
    public async Task CompleteDevelopmentTaskAsync_RejectsUserWhoIsNotAssignee()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-TASK-OWNER", "Task owner guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Protect task completion", "Only assignee can complete", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");
        var assigned = await service.AssignDevelopmentTaskAsync(
            tasks[0].Id,
            new AssignSprintDevelopmentTaskRequest("dev-1"),
            "pm-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteDevelopmentTaskAsync(assigned.Id, "dev-2"));

        Assert.Equal("Task is not assigned to current user.", exception.Message);
    }

    [Fact]
    public async Task ListRequirementReviews_ReturnsRejectionCommentsForRequirementDetail()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-REJECT", "MVP reject path"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Review rejected requirement", "Needs review", 1),
            "po-1");

        requirement = await service.SubmitRequirementReviewAsync(
            requirement.Id,
            new SubmitSprintRequirementReviewRequest(["arch-1"]),
            "po-1");
        await service.RejectRequirementReviewAsync(
            requirement.Id,
            "arch-1",
            new DecideSprintRequirementReviewRequest("Acceptance criteria are incomplete."));

        var reviews = await service.ListRequirementReviewsAsync(requirement.Id);

        var review = Assert.Single(reviews);
        Assert.Equal(SprintRequirementReviewStatuses.Rejected, review.Status);
        Assert.Equal("Acceptance criteria are incomplete.", review.Comment);
        Assert.Equal("arch-1", review.ReviewerId);
    }

    [Fact]
    public async Task UpdateRequirementAsync_RejectsUserWhoIsNotRequirementCreator()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-UPDATE-OWNER", "Update owner guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Protect requirement edit", "Only owner edits", 1),
            "po-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRequirementAsync(
                requirement.Id,
                new UpdateSprintRequirementRequest("Changed", "Changed body", 2, "arch-1"),
                "po-2"));

        Assert.Equal("Only requirement creator can update requirement content.", exception.Message);
    }

    [Fact]
    public async Task UpdateRequirementAsync_RejectsRequirementAfterSubmittedForReview()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-UPDATE-REVIEWED", "Update reviewed guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Reviewed requirement", "Already submitted", 1),
            "po-1");
        requirement = await service.SubmitRequirementReviewAsync(
            requirement.Id,
            new SubmitSprintRequirementReviewRequest(["arch-1"]),
            "po-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRequirementAsync(
                requirement.Id,
                new UpdateSprintRequirementRequest("Changed", "Changed body", 2, "arch-1"),
                "po-1"));

        Assert.Equal("Requirement status does not allow this operation.", exception.Message);
    }

    [Fact]
    public async Task SubmitRequirementReviewAsync_RejectsUserWhoIsNotRequirementCreator()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-SUBMIT-OWNER", "Submit owner guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Protect review submission", "Only owner submits", 1),
            "po-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitRequirementReviewAsync(
                requirement.Id,
                new SubmitSprintRequirementReviewRequest(["arch-1"]),
                "po-2"));

        Assert.Equal("Only requirement creator can submit requirement review.", exception.Message);
    }

    [Fact]
    public async Task DeleteDraftRequirementAsync_AllowsRequirementCreatorToDeleteDraftRequirement()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DELETE-DRAFT", "Delete draft path"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Delete stale draft", "Not ready to submit", 1),
            "po-1");

        var deleted = await service.DeleteDraftRequirementAsync(requirement.Id, "po-1");
        var requirements = await service.ListRequirementsAsync(project.Id);

        Assert.True(deleted);
        Assert.Empty(requirements);
    }

    [Fact]
    public async Task DeleteDraftRequirementAsync_RejectsRequirementAfterSubmittedForReview()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DELETE-SUBMITTED", "Delete submitted guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Submitted requirement", "Already in review", 1),
            "po-1");
        requirement = await service.SubmitRequirementReviewAsync(
            requirement.Id,
            new SubmitSprintRequirementReviewRequest(["arch-1"]),
            "po-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteDraftRequirementAsync(requirement.Id, "po-1"));

        Assert.Equal("Requirement status does not allow this operation.", exception.Message);
    }

    [Fact]
    public async Task DeleteDraftRequirementAsync_RejectsUserWhoIsNotRequirementCreator()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DELETE-OWNER", "Delete owner guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Protect draft", "Only owner can delete", 1),
            "po-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteDraftRequirementAsync(requirement.Id, "pm-2"));

        Assert.Equal("Only requirement creator can delete a draft requirement.", exception.Message);
    }

    [Fact]
    public async Task DeleteFeatureModuleAsync_AllowsDeletingModuleWithoutRequirements()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DELETE-MODULE", "Delete unused module"),
            "pm-1");
        var endpoint = (await service.ListProjectEndpointsAsync(project.Id)).Single();
        var module = await service.CreateFeatureModuleAsync(
            new CreateSprintFeatureModuleRequest(
                project.Id,
                endpoint.Id,
                "UNUSED",
                "Unused",
                null),
            "pm-1");

        var deleted = await service.DeleteFeatureModuleAsync(module.Id);
        var modules = await service.ListFeatureModulesAsync(project.Id, endpoint.Id);

        Assert.True(deleted);
        Assert.DoesNotContain(modules, item => item.Id == module.Id);
    }

    [Fact]
    public async Task DeleteFeatureModuleAsync_RejectsModuleReferencedByRequirement()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DELETE-MODULE-GUARD", "Delete used module guard"),
            "pm-1");
        var endpoint = (await service.ListProjectEndpointsAsync(project.Id)).Single();
        var module = await service.CreateFeatureModuleAsync(
            new CreateSprintFeatureModuleRequest(
                project.Id,
                endpoint.Id,
                "USED",
                "Used",
                null),
            "pm-1");
        await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(
                project.Id,
                "Requirement bound to module",
                null,
                2,
                EndpointId: endpoint.Id,
                ModuleId: module.Id),
            "po-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteFeatureModuleAsync(module.Id));

        Assert.Equal("Module is used by requirements and cannot be deleted.", exception.Message);
    }

    [Fact]
    public async Task VoidRequirementAsync_AllowsRequirementCreatorToVoidRejectedRequirement()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-VOID-OWNER", "Void owner path"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Void rejected requirement", "Rejected by architect", 1),
            "po-1");
        requirement = await service.SubmitRequirementReviewAsync(
            requirement.Id,
            new SubmitSprintRequirementReviewRequest(["arch-1"]),
            "po-1");
        await service.RejectRequirementReviewAsync(
            requirement.Id,
            "arch-1",
            new DecideSprintRequirementReviewRequest("No acceptance criteria."));

        var voided = await service.VoidRequirementAsync(requirement.Id, "po-1");

        Assert.Equal(SprintRequirementStatuses.Voided, voided.Status);
        Assert.NotNull(voided.VoidedAt);
        Assert.Equal("voided", voided.Health);
    }

    [Fact]
    public async Task VoidRequirementAsync_RejectsUserWhoIsNotRequirementCreator()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-VOID-GUARD", "Void guard path"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Protect rejected requirement", "Only owner can void", 1),
            "po-1");
        requirement = await service.SubmitRequirementReviewAsync(
            requirement.Id,
            new SubmitSprintRequirementReviewRequest(["arch-1"]),
            "po-1");
        await service.RejectRequirementReviewAsync(
            requirement.Id,
            "arch-1",
            new DecideSprintRequirementReviewRequest("No acceptance criteria."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VoidRequirementAsync(requirement.Id, "pm-2"));

        Assert.Equal("Only requirement creator can void a rejected requirement.", exception.Message);
    }

    [Fact]
    public async Task CompleteDevelopmentTask_CompletesTaskAndMovesRequirementToReadyTestWhenAllTasksDone()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-TASK-DONE", "Task completion path"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Complete decomposed tasks", "Finish all work items", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");

        foreach (var task in tasks)
        {
            await service.AssignDevelopmentTaskAsync(
                task.Id,
                new AssignSprintDevelopmentTaskRequest("dev-1"),
                "pm-1");
            await service.CompleteDevelopmentTaskAsync(task.Id, "dev-1");
        }

        var completedTasks = await service.ListDevelopmentTasksAsync(project.Id, requirement.Id, "dev-1");
        var updatedRequirement = (await service.ListRequirementsAsync(project.Id)).Single();

        Assert.All(completedTasks, task => Assert.Equal(SprintDevelopmentTaskStatuses.Completed, task.Status));
        Assert.Equal(SprintRequirementStatuses.ReadyForTest, updatedRequirement.Status);
        Assert.NotNull(updatedRequirement.DevelopmentCompletedAt);
    }

    [Fact]
    public async Task CompleteDevelopmentTaskAsync_AdvancesRequirementWhenTaskQueryReturnsPersistedSnapshot()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var taskDomain = new SnapshotOnListSprintDevelopmentTaskDomain();
        var service = new AgileMvpService(
            new InMemorySprintProjectDomain(),
            new InMemorySprintProjectMemberDomain(),
            new InMemorySprintProjectEndpointDomain(),
            new InMemorySprintFeatureModuleDomain(),
            requirementDomain,
            new InMemorySprintSkillDomain(),
            new InMemorySprintFeatureSuggestionDomain(),
            new InMemorySprintRequirementFeedbackDomain(),
            new InMemorySprintRequirementReviewDomain(),
            taskDomain,
            new InMemorySprintBugDomain(),
            new InMemorySprintTaskLeaseDomain(),
            new InMemoryAgileRuntimeEnvironmentDomain(),
            new InMemoryGitRepositoryDomain(),
            new InMemoryGitAccountDomain(),
            new InMemoryAgilePromptTemplateDomain(),
            new InMemoryAgileTestPlanDomain(),
            new InMemoryAgileDigitalWorkerDomain(),
            new InMemoryAgileWorkerCommandDomain(),
            new RequirementDecompositionService(),
            new StaticSystemConfigurationService());
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-TASK-SNAPSHOT", "Task snapshot"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Complete snapshot task", "Finish work item", 1),
            "pm-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1", "pm-1");
        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "pm-1");
        foreach (var task in tasks)
        {
            await service.AssignDevelopmentTaskAsync(
                task.Id,
                new AssignSprintDevelopmentTaskRequest("dev-1"),
                "pm-1");
            await service.CompleteDevelopmentTaskAsync(task.Id, "dev-1");
        }

        var updatedRequirement = (await service.ListRequirementsAsync(project.Id)).Single();
        Assert.Equal(SprintRequirementStatuses.ReadyForTest, updatedRequirement.Status);
        Assert.NotNull(updatedRequirement.DevelopmentCompletedAt);
    }

    [Fact]
    public async Task DecomposeRequirementAsync_UsesInjectedDecompositionServiceOnce()
    {
        var decompositionService = new CapturingRequirementDecompositionService();
        var service = CreateService(decompositionService);
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-AI-HOOK", "AI hook path"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Use AI decomposition", "Split by agent skill", 1),
            "po-1");
        requirement = await SubmitAndApproveRequirementAsync(service, requirement.Id, "pm-1");

        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest("Prefer backend first.", TaskCount: 2),
            "po-1");

        Assert.True(decompositionService.WasCalled);
        Assert.Equal("Prefer backend first.", decompositionService.LastInstruction);
        Assert.Equal(2, decompositionService.LastTaskCount);
        var task = Assert.Single(tasks);
        Assert.Single(await service.ListDevelopmentTasksAsync(project.Id, requirement.Id, null));
        Assert.Equal("AI 拆解任务", task.Title);
    }

    [Fact]
    public async Task DecomposeRequirementAsync_RejectsDraftRequirement()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-DECOMPOSE-DRAFT", "Draft decompose guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Draft requirement", "Cannot split before review", 1),
            "po-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecomposeRequirementAsync(
                requirement.Id,
                new DecomposeSprintRequirementRequest(null),
                "po-1"));

        Assert.Equal("Requirement status does not allow this operation.", exception.Message);
    }

    [Fact]
    public async Task CreateRequirementAsync_CanSkipReviewAndDecomposeImmediately()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-SKIP-REVIEW", "Skip review"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(
                project.Id,
                "Skip review requirement",
                "Can be decomposed immediately.",
                1,
                RequiresReview: false),
            "po-1");

        var tasks = await service.DecomposeRequirementAsync(
            requirement.Id,
            new DecomposeSprintRequirementRequest(null),
            "po-1");

        Assert.Equal(SprintRequirementStatuses.Approved, requirement.Status);
        Assert.Equal("po-1", requirement.ReviewedBy);
        Assert.NotNull(requirement.ApprovedAt);
        Assert.NotEmpty(tasks);
    }

    [Fact]
    public async Task ApproveRequirementAsync_RejectsDraftRequirementToProtectReviewWorkflow()
    {
        var service = CreateService();
        var project = await service.CreateProjectAsync(
            CreateProjectRequest("MVP-APPROVE-GUARD", "Approve guard"),
            "pm-1");
        var requirement = await service.CreateRequirementAsync(
            new CreateSprintRequirementRequest(project.Id, "Guard review workflow", null, 1),
            "po-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveRequirementAsync(requirement.Id, "pm-1"));

        Assert.Equal("Requirement status does not allow this operation.", exception.Message);
    }

    private static AgileMvpService CreateService(
        IRequirementDecompositionService? decompositionService = null,
        InMemorySprintProjectMemberDomain? projectMemberDomain = null,
        InMemorySprintProjectEndpointDomain? endpointDomain = null,
        ISystemConfigurationService? configurationService = null,
        InMemoryGitRepositoryDomain? gitRepositoryDomain = null,
        InMemoryGitAccountDomain? gitAccountDomain = null,
        InMemoryAgileDigitalWorkerDomain? digitalWorkerDomain = null,
        InMemoryAgileWorkerCommandDomain? workerCommandDomain = null)
    {
        return new AgileMvpService(
            new InMemorySprintProjectDomain(),
            projectMemberDomain ?? new InMemorySprintProjectMemberDomain(),
            endpointDomain ?? new InMemorySprintProjectEndpointDomain(),
            new InMemorySprintFeatureModuleDomain(),
            new InMemorySprintRequirementDomain(),
            new InMemorySprintSkillDomain(),
            new InMemorySprintFeatureSuggestionDomain(),
            new InMemorySprintRequirementFeedbackDomain(),
            new InMemorySprintRequirementReviewDomain(),
            new InMemorySprintDevelopmentTaskDomain(),
            new InMemorySprintBugDomain(),
            new InMemorySprintTaskLeaseDomain(),
            new InMemoryAgileRuntimeEnvironmentDomain(),
            gitRepositoryDomain ?? new InMemoryGitRepositoryDomain(),
            gitAccountDomain ?? new InMemoryGitAccountDomain(),
            new InMemoryAgilePromptTemplateDomain(),
            new InMemoryAgileTestPlanDomain(),
            digitalWorkerDomain ?? new InMemoryAgileDigitalWorkerDomain(),
            workerCommandDomain ?? new InMemoryAgileWorkerCommandDomain(),
            decompositionService ?? new RequirementDecompositionService(),
            configurationService ?? new StaticSystemConfigurationService());
    }

    private static CreateSprintProjectRequest CreateProjectRequest(
        string code,
        string name,
        string? testEnvironmentUrl = null,
        string description = "Project detail",
        string frontendTechStack = "Vue 3 / Vite / TDesign",
        string backendTechStack = ".NET 10 / EF Core / MySQL",
        string projectManagerId = "manager-1",
        IReadOnlyList<string>? productManagerIds = null,
        IReadOnlyList<string>? developerIds = null,
        IReadOnlyList<string>? testerIds = null,
        string architectId = "arch-1",
        string? gitRepositoryId = null,
        string? gitAccountId = null)
    {
        return new CreateSprintProjectRequest(
            code,
            name,
            testEnvironmentUrl,
            description,
            frontendTechStack,
            backendTechStack,
            projectManagerId,
            productManagerIds ?? ["pm-1"],
            developerIds ?? ["dev-1"],
            testerIds ?? ["tester-1"],
            architectId,
            null,
            gitRepositoryId,
            gitAccountId);
    }

    private static async Task<SprintRequirementResult> SubmitAndApproveRequirementAsync(
        AgileMvpService service,
        string requirementId,
        string reviewerId,
        string creatorId = "po-1")
    {
        await service.SubmitRequirementReviewAsync(
            requirementId,
            new SubmitSprintRequirementReviewRequest([reviewerId]),
            creatorId);
        var reviewed = await service.ApproveRequirementReviewAsync(
            requirementId,
            reviewerId,
            new DecideSprintRequirementReviewRequest("ok"));
        Assert.Equal(SprintRequirementStatuses.Approved, reviewed.Status);
        return await service.ApproveRequirementAsync(requirementId, reviewerId);
    }
}

internal sealed class CapturingRequirementDecompositionService : IRequirementDecompositionService
{
    public string? LastInstruction { get; private set; }

    public int? LastTaskCount { get; private set; }

    public bool WasCalled { get; private set; }

    public Task<IReadOnlyList<SprintDevelopmentTaskEntity>> DecomposeAsync(
        SprintRequirementEntity requirement,
        string? instruction,
        string userId,
        int? taskCount = null)
    {
        WasCalled = true;
        LastInstruction = instruction;
        LastTaskCount = taskCount;
        IReadOnlyList<SprintDevelopmentTaskEntity> tasks =
        [
            new SprintDevelopmentTaskEntity
            {
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                Title = "AI 拆解任务",
                Description = requirement.Description,
                CreatedBy = userId
            }
        ];
        return Task.FromResult(tasks);
    }
}

internal sealed class StaticSystemConfigurationService : ISystemConfigurationService
{
    private readonly string? _key;
    private readonly string? _value;

    public StaticSystemConfigurationService(string? key = null, string? value = null)
    {
        _key = key;
        _value = value;
    }

    public Task<IReadOnlyList<SystemConfigurationResult>> ListConfigurationsAsync(string? keyword = null, int? status = null)
    {
        return Task.FromResult<IReadOnlyList<SystemConfigurationResult>>([]);
    }

    public Task<SystemConfigurationResult> UpsertConfigurationAsync(UpsertSystemConfigurationRequest request)
    {
        return Task.FromResult(new SystemConfigurationResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.Key,
            request.Value,
            request.Description,
            request.Status));
    }

    public Task<bool> DeleteConfigurationAsync(string id)
    {
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<AiPlatformResult>> ListAiPlatformsAsync(string? keyword = null, int? status = null)
    {
        return Task.FromResult<IReadOnlyList<AiPlatformResult>>([]);
    }

    public Task<AiPlatformResult> UpsertAiPlatformAsync(UpsertAiPlatformRequest request)
    {
        return Task.FromResult(new AiPlatformResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.Code,
            request.Name,
            request.Provider,
            request.Model,
            request.OpenAiBaseUrl,
            request.Description,
            request.Sort,
            request.Status));
    }

    public Task<bool> DeleteAiPlatformAsync(string id)
    {
        return Task.FromResult(true);
    }

    public Task<string> GetValueAsync(string key, string defaultValue)
    {
        return Task.FromResult(_key == key && !string.IsNullOrWhiteSpace(_value) ? _value : defaultValue);
    }
}

internal sealed class InMemorySprintProjectDomain : InMemoryDomainBase<SprintProjectEntity>, ISprintProjectDomain;

internal sealed class InMemoryGitRepositoryDomain : InMemoryDomainBase<GitRepositoryEntity>, IGitRepositoryDomain;

internal sealed class InMemoryGitAccountDomain : InMemoryDomainBase<GitAccountEntity>, IGitAccountDomain;

internal sealed class InMemoryGitBranchOperationDomain :
    InMemoryDomainBase<GitBranchOperationEntity>,
    IGitBranchOperationDomain;

internal sealed class InMemorySprintProjectMemberDomain :
    InMemoryDomainBase<SprintProjectMemberEntity>,
    ISprintProjectMemberDomain;

internal sealed class InMemorySprintProjectEndpointDomain :
    InMemoryDomainBase<SprintProjectEndpointEntity>,
    ISprintProjectEndpointDomain;

internal sealed class InMemorySprintFeatureModuleDomain :
    InMemoryDomainBase<SprintFeatureModuleEntity>,
    ISprintFeatureModuleDomain;

internal sealed class InMemorySprintRequirementDomain : InMemoryDomainBase<SprintRequirementEntity>, ISprintRequirementDomain;

internal sealed class InMemorySprintSkillDomain : InMemoryDomainBase<SprintSkillEntity>, ISprintSkillDomain;

internal sealed class InMemorySprintFeatureSuggestionDomain :
    InMemoryDomainBase<SprintFeatureSuggestionEntity>,
    ISprintFeatureSuggestionDomain;

internal sealed class InMemorySprintRequirementFeedbackDomain :
    InMemoryDomainBase<SprintRequirementFeedbackEntity>,
    ISprintRequirementFeedbackDomain;

internal sealed class InMemorySprintRequirementReviewDomain :
    InMemoryDomainBase<SprintRequirementReviewEntity>,
    ISprintRequirementReviewDomain;

internal sealed class SnapshotOnListSprintRequirementReviewDomain :
    InMemoryDomainBase<SprintRequirementReviewEntity>,
    ISprintRequirementReviewDomain
{
    public new async Task<IList<SprintRequirementReviewEntity>> ListAsync(
        Expression<Func<SprintRequirementReviewEntity, bool>>? predicate = null)
    {
        var entities = await base.ListAsync(predicate);
        return entities.Select(entity => new SprintRequirementReviewEntity
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            RequirementId = entity.RequirementId,
            ReviewerId = entity.ReviewerId,
            Status = entity.Status,
            Comment = entity.Comment,
            ReviewedAt = entity.ReviewedAt,
            CreateTime = entity.CreateTime,
            UpdateTime = entity.UpdateTime,
            IsDelete = entity.IsDelete
        }).ToList();
    }
}

internal sealed class InMemorySprintDevelopmentTaskDomain :
    InMemoryDomainBase<SprintDevelopmentTaskEntity>,
    ISprintDevelopmentTaskDomain;

internal sealed class SnapshotOnListSprintDevelopmentTaskDomain :
    InMemoryDomainBase<SprintDevelopmentTaskEntity>,
    ISprintDevelopmentTaskDomain
{
    public new async Task<IList<SprintDevelopmentTaskEntity>> ListAsync(
        Expression<Func<SprintDevelopmentTaskEntity, bool>>? predicate = null)
    {
        var entities = await base.ListAsync(predicate);
        return entities.Select(entity => new SprintDevelopmentTaskEntity
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            RequirementId = entity.RequirementId,
            Title = entity.Title,
            Description = entity.Description,
            Status = entity.Status,
            Priority = entity.Priority,
            AssigneeId = entity.AssigneeId,
            AssigneeType = entity.AssigneeType,
            AssignedBy = entity.AssignedBy,
            CreatedBy = entity.CreatedBy,
            Prompt = entity.Prompt,
            AssignedAt = entity.AssignedAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            CreateTime = entity.CreateTime,
            UpdateTime = entity.UpdateTime,
            IsDelete = entity.IsDelete
        }).ToList();
    }
}

internal sealed class InMemorySprintBugDomain : InMemoryDomainBase<SprintBugEntity>, ISprintBugDomain;

internal sealed class InMemorySprintTaskLeaseDomain : InMemoryDomainBase<SprintTaskLeaseEntity>, ISprintTaskLeaseDomain
{
    public new async Task<string> CreateAsync(SprintTaskLeaseEntity entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.ActiveTargetKey))
        {
            var existing = await ListAsync(item =>
                item.ActiveTargetKey == entity.ActiveTargetKey &&
                item.IsDelete == 0);
            if (existing.Count > 0)
            {
                throw new InvalidOperationException("IX_sprint_task_lease_ActiveTargetKey");
            }
        }

        return await base.CreateAsync(entity);
    }
}

internal sealed class InMemoryAgileRuntimeEnvironmentDomain :
    InMemoryDomainBase<RuntimeEnvironmentEntity>,
    IRuntimeEnvironmentDomain;

internal sealed class InMemoryAgilePromptTemplateDomain : InMemoryDomainBase<PromptTemplateEntity>, IPromptTemplateDomain
{
    public InMemoryAgilePromptTemplateDomain()
    {
        CreateAsync(new PromptTemplateEntity
        {
            AgentEnvironment = "codex",
            Code = "mcp_setup",
            Name = "MCP 接入配置",
            Content = """
                      MCP {{projectCode}} {{taskId}}

                      [mcp_servers.agentsprint]
                      url = "{{mcpEndpoint}}"
                      http_headers = { Authorization = "{{agentToken}}" }

                      Token placeholder: Bearer <这里填令牌>
                      """,
            Description = "首次接入",
            Sort = 1,
            Status = 1
        }).GetAwaiter().GetResult();
        CreateAsync(new PromptTemplateEntity
        {
            AgentEnvironment = "codex",
            Code = "task_execution",
            Name = "任务推进提示词",
            Content = "Task {{projectCode}} {{taskId}} {{repositoryReference}} {{workspacePath}} stop-after-complete",
            Description = "日常推进",
            Sort = 2,
            Status = 1
        }).GetAwaiter().GetResult();
    }
}

internal sealed class InMemoryAgileTestPlanDomain : InMemoryDomainBase<TestPlanEntity>, ITestPlanDomain;

internal sealed class InMemoryAgileDigitalWorkerDomain :
    InMemoryDomainBase<DigitalWorkerEntity>,
    IDigitalWorkerDomain;

internal sealed class InMemoryAgileWorkerCommandDomain :
    InMemoryDomainBase<WorkerCommandEntity>,
    IWorkerCommandDomain;

