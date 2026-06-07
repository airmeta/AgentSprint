using System.Linq.Expressions;

using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Tests;
using AgentSprint.Model.Modules.Tests.Domains;
using AgentSprint.Model.Modules.Tests.Dtos;
using AgentSprint.Service.Impls.TestServices;

namespace AgentSprint.Tests;

public sealed class TestServiceTests
{
    [Fact]
    public async Task CreatePlanAsync_DefaultsEnvironmentAndTrimsOptionalFields()
    {
        var planDomain = new InMemoryTestPlanDomain();
        var bugDomain = new InMemorySprintBugDomain();
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(
            planDomain,
            new InMemoryTestExecutionDomain(),
            bugDomain,
            requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        await bugDomain.CreateAsync(new SprintBugEntity
        {
            Id = "BUG-1",
            ProjectId = "PROJ",
            RequirementId = "REQ-1",
            Title = "Existing regression bug",
            Status = SprintBugStatuses.Open,
            CreatedBy = "tester-1"
        });

        var result = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", null, " http://localhost:5999 ", " BUG-1 "),
            "tester-1");

        Assert.Equal("test", result.Environment);
        Assert.Equal("http://localhost:5999", result.TestUrl);
        Assert.Equal("BUG-1", result.BugId);
        Assert.Equal("tester-1", result.CreatedBy);
        Assert.Equal(TestPlanStatuses.Pending, result.Status);
        Assert.Single(await planDomain.ListAsync());
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsMissingRequiredFields()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlanAsync(new CreateTestPlanRequest("PROJ", "", "First round", null, null, null), "tester-1"));

        Assert.Equal("ProjectId, RequirementId and Name are required.", exception.Message);
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsMissingRequirement()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlanAsync(
                new CreateTestPlanRequest("PROJ", "REQ-MISSING", "First round", null, null, null),
                "tester-1"));

        Assert.Equal("Requirement does not exist.", exception.Message);
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsRequirementFromDifferentProject()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain, projectId: "OTHER");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlanAsync(
                new CreateTestPlanRequest("PROJ", "REQ-1", "First round", null, null, null),
                "tester-1"));

        Assert.Equal("Test plan project does not match requirement project.", exception.Message);
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsRequirementThatIsNotReadyForTesting()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain, status: SprintRequirementStatuses.Draft);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlanAsync(
                new CreateTestPlanRequest("PROJ", "REQ-1", "First round", null, null, null),
                "tester-1"));

        Assert.Equal("Requirement is not ready for testing.", exception.Message);
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsMissingLinkedBug()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlanAsync(
                new CreateTestPlanRequest("PROJ", "REQ-1", "Regression round", null, null, "BUG-MISSING"),
                "tester-1"));

        Assert.Equal("Linked bug does not exist.", exception.Message);
    }

    [Fact]
    public async Task CreatePlanAsync_RejectsLinkedBugFromDifferentRequirement()
    {
        var bugDomain = new InMemorySprintBugDomain();
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(bugDomain: bugDomain, requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        await bugDomain.CreateAsync(new SprintBugEntity
        {
            Id = "BUG-OTHER",
            ProjectId = "PROJ",
            RequirementId = "REQ-OTHER",
            Title = "Other requirement bug",
            Status = SprintBugStatuses.Open,
            CreatedBy = "tester-1"
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlanAsync(
                new CreateTestPlanRequest("PROJ", "REQ-1", "Regression round", null, null, "BUG-OTHER"),
                "tester-1"));

        Assert.Equal("Linked bug does not belong to this test plan requirement.", exception.Message);
    }

    [Fact]
    public async Task CreatePlanAsync_AddsTesterProjectMember()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var projectMemberDomain = new InMemorySprintProjectMemberDomain();
        var service = CreateService(
            requirementDomain: requirementDomain,
            projectMemberDomain: projectMemberDomain);
        await CreateRequirementAsync(requirementDomain);

        await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", null, null, null),
            "tester-1");

        var members = await projectMemberDomain.ListAsync();
        var member = Assert.Single(members);
        Assert.Equal("PROJ", member.ProjectId);
        Assert.Equal("tester-1", member.UserId);
        Assert.Equal(SprintProjectMemberRoles.Tester, member.Role);
    }

    [Fact]
    public async Task SubmitExecutionAsync_BindsCreatedBugAndUpdatesPlanStatus()
    {
        var planDomain = new InMemoryTestPlanDomain();
        var executionDomain = new InMemoryTestExecutionDomain();
        var bugDomain = new InMemorySprintBugDomain();
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(planDomain, executionDomain, bugDomain, requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        await bugDomain.CreateAsync(new SprintBugEntity
        {
            Id = "BUG-2",
            ProjectId = "PROJ",
            RequirementId = "REQ-1",
            Title = "Created from failed execution",
            Status = SprintBugStatuses.Open,
            CreatedBy = "tester-1"
        });
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");
        plan = await service.StartPlanAsync(plan.Id);

        var execution = await service.SubmitExecutionAsync(
            plan.Id,
            new SubmitTestExecutionRequest("FAILED", "Page returned 500", "screenshot:500", null, "BUG-2"),
            "tester-1");

        Assert.Equal(TestExecutionResults.Failed, execution.Result);
        Assert.Equal("REQ-1", execution.RequirementId);
        Assert.Equal("tester-1", execution.TesterId);
        Assert.Equal("BUG-2", execution.CreatedBugId);

        var updatedPlan = await planDomain.GetAsync(plan.Id);
        var boundBug = await bugDomain.GetAsync("BUG-2");
        var updatedRequirement = await requirementDomain.GetAsync("REQ-1");
        Assert.NotNull(updatedPlan);
        Assert.Equal(TestPlanStatuses.Failed, updatedPlan.Status);
        Assert.NotNull(updatedPlan.CompletedAt);
        Assert.NotNull(boundBug);
        Assert.Equal(plan.Id, boundBug.TestPlanId);
        Assert.Equal(execution.Id, boundBug.TestExecutionId);
        Assert.NotNull(updatedRequirement);
        Assert.Equal(SprintRequirementStatuses.PendingFix, updatedRequirement.Status);
        Assert.Single(await executionDomain.ListAsync(entity => entity.TestPlanId == plan.Id));
    }

    [Fact]
    public async Task StartPlanAsync_MarksRequirementTesting()
    {
        var planDomain = new InMemoryTestPlanDomain();
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(planDomain, requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");

        await service.StartPlanAsync(plan.Id);

        var requirement = await requirementDomain.GetAsync("REQ-1");
        Assert.NotNull(requirement);
        Assert.Equal(SprintRequirementStatuses.Testing, requirement.Status);
    }

    [Fact]
    public async Task StartPlanAsync_RejectsPlanThatAlreadyStarted()
    {
        var planDomain = new InMemoryTestPlanDomain();
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(planDomain, requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");
        await service.StartPlanAsync(plan.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartPlanAsync(plan.Id));

        Assert.Equal("Only pending test plans can be started.", exception.Message);
    }

    [Fact]
    public async Task SubmitExecutionAsync_MarksRequirementTestedWhenPassed()
    {
        var planDomain = new InMemoryTestPlanDomain();
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(planDomain, new InMemoryTestExecutionDomain(), requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");
        plan = await service.StartPlanAsync(plan.Id);

        await service.SubmitExecutionAsync(
            plan.Id,
            new SubmitTestExecutionRequest("passed", "Accepted", "report-url", null, null),
            "tester-1");

        var requirement = await requirementDomain.GetAsync("REQ-1");
        Assert.NotNull(requirement);
        Assert.Equal(SprintRequirementStatuses.Tested, requirement.Status);
        Assert.NotNull(requirement.TestedAt);
    }

    [Fact]
    public async Task SubmitExecutionAsync_RejectsPlanThatHasNotStartedTesting()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitExecutionAsync(
                plan.Id,
                new SubmitTestExecutionRequest("passed", "ok", null, null, null),
                "tester-1"));

        Assert.Equal("Test execution can only be submitted while the plan is testing.", exception.Message);
    }

    [Fact]
    public async Task CompletePlanAsync_RejectsNonTerminalStatus()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompletePlanAsync(plan.Id, new CompleteTestPlanRequest(TestPlanStatuses.Testing, null)));

        Assert.Equal("Completed test plan status must be passed, failed, blocked or closed.", exception.Message);
    }

    [Fact]
    public async Task UpdateExecutionBugAsync_WritesCreatedBugAfterExecutionIsSaved()
    {
        var bugDomain = new InMemorySprintBugDomain();
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(bugDomain: bugDomain, requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        await bugDomain.CreateAsync(new SprintBugEntity
        {
            Id = "BUG-PENDING",
            ProjectId = "PROJ",
            RequirementId = "REQ-1",
            Title = "Temporary linked bug",
            Status = SprintBugStatuses.Open,
            CreatedBy = "tester-1"
        });
        await bugDomain.CreateAsync(new SprintBugEntity
        {
            Id = "BUG-100",
            ProjectId = "PROJ",
            RequirementId = "REQ-1",
            Title = "Created after execution",
            Status = SprintBugStatuses.Open,
            CreatedBy = "tester-1"
        });
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");
        plan = await service.StartPlanAsync(plan.Id);
        var execution = await service.SubmitExecutionAsync(
            plan.Id,
            new SubmitTestExecutionRequest("failed", "Page returned 500", null, null, "BUG-PENDING"),
            "tester-1");

        var updated = await service.UpdateExecutionBugAsync(
            plan.Id,
            execution.Id,
            new UpdateTestExecutionBugRequest(null, "BUG-100"));

        Assert.Equal("BUG-100", updated.CreatedBugId);
        Assert.Null(updated.BugId);
        var bug = await bugDomain.GetAsync("BUG-100");
        Assert.NotNull(bug);
        Assert.Equal(plan.Id, bug.TestPlanId);
        Assert.Equal(execution.Id, bug.TestExecutionId);
    }

    [Fact]
    public async Task SubmitExecutionAsync_RejectsCreatedBugThatDoesNotExist()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");
        plan = await service.StartPlanAsync(plan.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitExecutionAsync(
                plan.Id,
                new SubmitTestExecutionRequest("failed", "Page returned 500", null, null, "BUG-MISSING"),
                "tester-1"));

        Assert.Equal("Created bug does not exist.", exception.Message);
    }

    [Fact]
    public async Task SubmitExecutionAsync_RejectsCreatedBugFromDifferentRequirementBeforeSavingExecution()
    {
        var executionDomain = new InMemoryTestExecutionDomain();
        var bugDomain = new InMemorySprintBugDomain();
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(
            new InMemoryTestPlanDomain(),
            executionDomain,
            bugDomain,
            requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        await bugDomain.CreateAsync(new SprintBugEntity
        {
            Id = "BUG-OTHER",
            ProjectId = "PROJ",
            RequirementId = "REQ-OTHER",
            Title = "Wrong requirement bug",
            Status = SprintBugStatuses.Open,
            CreatedBy = "tester-1"
        });
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");
        plan = await service.StartPlanAsync(plan.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitExecutionAsync(
                plan.Id,
                new SubmitTestExecutionRequest("failed", "Page returned 500", null, null, "BUG-OTHER"),
                "tester-1"));

        Assert.Equal("Created bug does not belong to this test plan requirement.", exception.Message);
        Assert.Empty(await executionDomain.ListAsync());
    }

    [Fact]
    public async Task SubmitExecutionAsync_RejectsFailedResultWithoutBugLink()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(requirementDomain: requirementDomain);
        await CreateRequirementAsync(requirementDomain);
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");
        plan = await service.StartPlanAsync(plan.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitExecutionAsync(
                plan.Id,
                new SubmitTestExecutionRequest("failed", "Page returned 500", null, null, null),
                "tester-1"));

        Assert.Equal("Failed test execution must link an existing bug or a created bug.", exception.Message);
    }

    [Fact]
    public async Task SubmitExecutionAsync_ReopensLinkedBugWhenRegressionFails()
    {
        var planDomain = new InMemoryTestPlanDomain();
        var executionDomain = new InMemoryTestExecutionDomain();
        var bugDomain = new InMemorySprintBugDomain();
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(planDomain, executionDomain, bugDomain, requirementDomain);
        var requirement = new SprintRequirementEntity
        {
            Id = "REQ-1",
            ProjectId = "PROJ",
            Status = SprintRequirementStatuses.Testing,
            Title = "Regression target",
            CreatedBy = "po-1"
        };
        await requirementDomain.CreateAsync(requirement);
        var bug = new SprintBugEntity
        {
            Id = "BUG-1",
            ProjectId = "PROJ",
            RequirementId = "REQ-1",
            Title = "Existing regression bug",
            Status = SprintBugStatuses.FixedReadyForRegression,
            CreatedBy = "tester-1",
            FixedAt = DateTime.UtcNow
        };
        await bugDomain.CreateAsync(bug);
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "Regression round", "test", null, "BUG-1"),
            "creator-1");
        plan = await service.StartPlanAsync(plan.Id);

        var execution = await service.SubmitExecutionAsync(
            plan.Id,
            new SubmitTestExecutionRequest("failed", "Regression still fails", "screenshot", "BUG-1", null),
            "tester-1");

        var reopenedBug = await bugDomain.GetAsync("BUG-1");
        var updatedRequirement = await requirementDomain.GetAsync("REQ-1");
        Assert.Equal("BUG-1", execution.BugId);
        Assert.NotNull(reopenedBug);
        Assert.Equal(SprintBugStatuses.Open, reopenedBug.Status);
        Assert.Null(reopenedBug.FixedAt);
        Assert.NotNull(updatedRequirement);
        Assert.Equal(SprintRequirementStatuses.PendingFix, updatedRequirement.Status);
    }

    [Fact]
    public async Task SubmitExecutionAsync_AddsTesterProjectMember()
    {
        var requirementDomain = new InMemorySprintRequirementDomain();
        var projectMemberDomain = new InMemorySprintProjectMemberDomain();
        var service = CreateService(
            new InMemoryTestPlanDomain(),
            new InMemoryTestExecutionDomain(),
            requirementDomain: requirementDomain,
            projectMemberDomain: projectMemberDomain);
        await CreateRequirementAsync(requirementDomain);
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "First round", "test", null, null),
            "creator-1");
        plan = await service.StartPlanAsync(plan.Id);

        await service.SubmitExecutionAsync(
            plan.Id,
            new SubmitTestExecutionRequest("passed", "Accepted", "report-url", null, null),
            "tester-1");

        var members = await projectMemberDomain.ListAsync(entity =>
            entity.ProjectId == "PROJ" &&
            entity.UserId == "tester-1" &&
            entity.Role == SprintProjectMemberRoles.Tester);
        Assert.Single(members);
    }

    [Fact]
    public async Task UpdateExecutionBugAsync_ReopensLinkedBugWhenFailedExecutionGetsExistingBug()
    {
        var planDomain = new InMemoryTestPlanDomain();
        var executionDomain = new InMemoryTestExecutionDomain();
        var bugDomain = new InMemorySprintBugDomain();
        var requirementDomain = new InMemorySprintRequirementDomain();
        var service = CreateService(planDomain, executionDomain, bugDomain, requirementDomain);
        await requirementDomain.CreateAsync(new SprintRequirementEntity
        {
            Id = "REQ-1",
            ProjectId = "PROJ",
            Status = SprintRequirementStatuses.Testing,
            Title = "Regression target",
            CreatedBy = "po-1"
        });
        await bugDomain.CreateAsync(new SprintBugEntity
        {
            Id = "BUG-2",
            ProjectId = "PROJ",
            RequirementId = "REQ-1",
            Title = "Existing regression bug",
            Status = SprintBugStatuses.FixedReadyForRegression,
            CreatedBy = "tester-1",
            FixedAt = DateTime.UtcNow
        });
        await bugDomain.CreateAsync(new SprintBugEntity
        {
            Id = "BUG-PENDING",
            ProjectId = "PROJ",
            RequirementId = "REQ-1",
            Title = "Temporary created bug",
            Status = SprintBugStatuses.Open,
            CreatedBy = "tester-1"
        });
        var plan = await service.CreatePlanAsync(
            new CreateTestPlanRequest("PROJ", "REQ-1", "Regression round", "test", null, null),
            "creator-1");
        plan = await service.StartPlanAsync(plan.Id);
        var execution = await service.SubmitExecutionAsync(
            plan.Id,
            new SubmitTestExecutionRequest("failed", "Regression still fails", null, null, "BUG-PENDING"),
            "tester-1");

        await service.UpdateExecutionBugAsync(
            plan.Id,
            execution.Id,
            new UpdateTestExecutionBugRequest("BUG-2", null));

        var reopenedBug = await bugDomain.GetAsync("BUG-2");
        var updatedRequirement = await requirementDomain.GetAsync("REQ-1");
        Assert.NotNull(reopenedBug);
        Assert.Equal(SprintBugStatuses.Open, reopenedBug.Status);
        Assert.Null(reopenedBug.FixedAt);
        Assert.NotNull(updatedRequirement);
        Assert.Equal(SprintRequirementStatuses.PendingFix, updatedRequirement.Status);
    }

    private static TestService CreateService(
        InMemoryTestPlanDomain? planDomain = null,
        InMemoryTestExecutionDomain? executionDomain = null,
        InMemorySprintBugDomain? bugDomain = null,
        InMemorySprintRequirementDomain? requirementDomain = null,
        InMemorySprintProjectMemberDomain? projectMemberDomain = null)
    {
        return new TestService(
            planDomain ?? new InMemoryTestPlanDomain(),
            executionDomain ?? new InMemoryTestExecutionDomain(),
            bugDomain ?? new InMemorySprintBugDomain(),
            projectMemberDomain ?? new InMemorySprintProjectMemberDomain(),
            requirementDomain ?? new InMemorySprintRequirementDomain());
    }

    private static Task<string> CreateRequirementAsync(
        InMemorySprintRequirementDomain requirementDomain,
        string id = "REQ-1",
        string projectId = "PROJ",
        string status = SprintRequirementStatuses.ReadyForTest)
    {
        return requirementDomain.CreateAsync(new SprintRequirementEntity
        {
            Id = id,
            ProjectId = projectId,
            Status = status,
            Title = "First requirement",
            CreatedBy = "po-1"
        });
    }
}

internal sealed class InMemoryTestPlanDomain : InMemoryDomainBase<TestPlanEntity>, ITestPlanDomain;

internal sealed class InMemoryTestExecutionDomain : InMemoryDomainBase<TestExecutionEntity>, ITestExecutionDomain;

internal abstract class InMemoryDomainBase<TEntity>
    where TEntity : AgentSprint.Model.Modules.Common.EntityBase, new()
{
    private readonly List<TEntity> _entities = [];

    public Task<string> CreateAsync(TEntity entity)
    {
        _entities.Add(entity);
        return Task.FromResult(entity.Id);
    }

    public Task<TEntity?> GetAsync(string id)
    {
        return Task.FromResult(_entities.SingleOrDefault(entity => entity.Id == id && entity.IsDelete == 0));
    }

    public Task<IList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null)
    {
        IQueryable<TEntity> query = _entities.AsQueryable().Where(entity => entity.IsDelete == 0);
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return Task.FromResult<IList<TEntity>>(query.ToList());
    }

    public Task<IList<TEntity>> ListIncludingDeletedAsync(Expression<Func<TEntity, bool>>? predicate = null)
    {
        IQueryable<TEntity> query = _entities.AsQueryable();
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return Task.FromResult<IList<TEntity>>(query.ToList());
    }

    public Task<string> UpdateAsync(TEntity entity)
    {
        entity.UpdateTime = DateTime.UtcNow;
        return Task.FromResult(entity.Id);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await GetAsync(id);
        if (entity is null)
        {
            return true;
        }

        entity.IsDelete = 1;
        entity.UpdateTime = DateTime.UtcNow;
        return true;
    }
}
