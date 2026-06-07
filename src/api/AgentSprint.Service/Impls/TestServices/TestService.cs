using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Tests;
using AgentSprint.Model.Modules.Tests.Domains;
using AgentSprint.Model.Modules.Tests.Dtos;
using AgentSprint.Service.Services.TestServices;

namespace AgentSprint.Service.Impls.TestServices;

public sealed class TestService : ITestService
{
    private static readonly HashSet<string> ValidPlanStatuses =
    [
        TestPlanStatuses.Pending,
        TestPlanStatuses.Testing,
        TestPlanStatuses.Passed,
        TestPlanStatuses.Failed,
        TestPlanStatuses.Blocked,
        TestPlanStatuses.Closed
    ];

    private static readonly HashSet<string> ValidExecutionResults =
    [
        TestExecutionResults.Passed,
        TestExecutionResults.Failed,
        TestExecutionResults.Blocked
    ];

    private static readonly HashSet<string> TestableRequirementStatuses =
    [
        SprintRequirementStatuses.ReadyForTest,
        SprintRequirementStatuses.Testing,
        SprintRequirementStatuses.Tested,
        SprintRequirementStatuses.PendingFix
    ];

    private readonly ITestPlanDomain _testPlanDomain;
    private readonly ITestExecutionDomain _testExecutionDomain;
    private readonly ISprintBugDomain _bugDomain;
    private readonly ISprintProjectMemberDomain _projectMemberDomain;
    private readonly ISprintRequirementDomain _requirementDomain;

    /// <summary>
    /// zh-cn: 创建测试服务，注入测试计划、测试执行、缺陷和需求领域对象以完成测试持久化，并在失败执行关联已有缺陷时把缺陷重新拉回修复流程。
    /// en-us: Creates the test service with test plan, test execution, bug, and requirement domains so test persistence can also return an existing linked bug to the fix flow when an execution fails.
    /// </summary>
    /// <param name="testPlanDomain">
    /// zh-cn: 测试计划领域对象。
    /// en-us: Test plan domain object.
    /// </param>
    /// <param name="testExecutionDomain">
    /// zh-cn: 测试执行领域对象。
    /// en-us: Test execution domain object.
    /// </param>
    /// <param name="bugDomain">
    /// zh-cn: 缺陷领域对象，用于失败执行关联已有缺陷时重开缺陷。
    /// en-us: Bug domain object used to reopen an existing bug linked by a failed execution.
    /// </param>
    /// <param name="requirementDomain">
    /// zh-cn: 需求领域对象，用于失败执行关联已有缺陷时把需求退回待修复。
    /// en-us: Requirement domain object used to move the requirement back to pending-fix when a failed execution links an existing bug.
    /// </param>
    public TestService(
        ITestPlanDomain testPlanDomain,
        ITestExecutionDomain testExecutionDomain,
        ISprintBugDomain bugDomain,
        ISprintProjectMemberDomain projectMemberDomain,
        ISprintRequirementDomain requirementDomain)
    {
        _testPlanDomain = testPlanDomain;
        _testExecutionDomain = testExecutionDomain;
        _bugDomain = bugDomain;
        _projectMemberDomain = projectMemberDomain;
        _requirementDomain = requirementDomain;
    }

    /// <inheritdoc />
    public async Task<TestPlanResult> CreatePlanAsync(CreateTestPlanRequest request, string userId)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId) ||
            string.IsNullOrWhiteSpace(request.RequirementId) ||
            string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("ProjectId, RequirementId and Name are required.");
        }

        var requirement = await _requirementDomain.GetAsync(request.RequirementId) ??
            throw new InvalidOperationException("Requirement does not exist.");
        if (!string.Equals(requirement.ProjectId, request.ProjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Test plan project does not match requirement project.");
        }

        EnsureTestableRequirementStatus(requirement);
        var bugId = NormalizeOptional(request.BugId);
        if (!string.IsNullOrWhiteSpace(bugId))
        {
            await EnsureBugBelongsToPlanRequirementAsync(
                bugId,
                request.ProjectId,
                request.RequirementId,
                "Linked bug");
        }

        var entity = new TestPlanEntity
        {
            ProjectId = request.ProjectId,
            RequirementId = request.RequirementId,
            BugId = bugId,
            TesterId = NormalizeOptional(request.TesterId),
            Name = request.Name,
            Environment = string.IsNullOrWhiteSpace(request.Environment) ? "test" : request.Environment,
            TestUrl = NormalizeOptional(request.TestUrl),
            CreatedBy = userId
        };

        await _testPlanDomain.CreateAsync(entity);
        await EnsureProjectMemberAsync(entity.ProjectId, userId, SprintProjectMemberRoles.Tester);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<TestPlanResult> StartPlanAsync(string id)
    {
        var entity = await GetPlanOrThrowAsync(id);
        if (entity.Status != TestPlanStatuses.Pending)
        {
            throw new InvalidOperationException("Only pending test plans can be started.");
        }

        var requirement = await _requirementDomain.GetAsync(entity.RequirementId) ??
            throw new InvalidOperationException("Requirement does not exist.");
        EnsureTestableRequirementStatus(requirement);

        entity.Status = TestPlanStatuses.Testing;
        entity.StartedAt ??= DateTime.UtcNow;
        await _testPlanDomain.UpdateAsync(entity);

        requirement.Status = SprintRequirementStatuses.Testing;
        await _requirementDomain.UpdateAsync(requirement);

        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<TestPlanResult> CompletePlanAsync(string id, CompleteTestPlanRequest request)
    {
        var status = NormalizeStatus(request.Status);
        if (status is TestPlanStatuses.Pending or TestPlanStatuses.Testing)
        {
            throw new InvalidOperationException("Completed test plan status must be passed, failed, blocked or closed.");
        }

        var entity = await GetPlanOrThrowAsync(id);
        entity.Status = status;
        entity.Summary = NormalizeOptional(request.Summary);
        entity.CompletedAt = DateTime.UtcNow;
        await _testPlanDomain.UpdateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TestPlanResult>> ListPlansAsync(string? projectId, string? requirementId)
    {
        var entities = await _testPlanDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(projectId) || entity.ProjectId == projectId) &&
            (string.IsNullOrWhiteSpace(requirementId) || entity.RequirementId == requirementId));

        return entities
            .OrderByDescending(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<TestExecutionResult> SubmitExecutionAsync(
        string testPlanId,
        SubmitTestExecutionRequest request,
        string userId)
    {
        var result = NormalizeExecutionResult(request.Result);
        var plan = await GetPlanOrThrowAsync(testPlanId);
        if (plan.Status != TestPlanStatuses.Testing)
        {
            throw new InvalidOperationException("Test execution can only be submitted while the plan is testing.");
        }

        var bugId = NormalizeOptional(request.BugId ?? plan.BugId);
        var createdBugId = NormalizeOptional(request.CreatedBugId);
        if (result == TestExecutionResults.Failed &&
            string.IsNullOrWhiteSpace(bugId) &&
            string.IsNullOrWhiteSpace(createdBugId))
        {
            throw new InvalidOperationException("Failed test execution must link an existing bug or a created bug.");
        }

        if (!string.IsNullOrWhiteSpace(bugId))
        {
            await EnsureBugBelongsToPlanRequirementAsync(
                bugId,
                plan.ProjectId,
                plan.RequirementId,
                "Linked bug");
        }

        if (!string.IsNullOrWhiteSpace(createdBugId))
        {
            await EnsureBugBelongsToPlanRequirementAsync(
                createdBugId,
                plan.ProjectId,
                plan.RequirementId,
                "Created bug");
        }

        var entity = new TestExecutionEntity
        {
            TestPlanId = plan.Id,
            RequirementId = plan.RequirementId,
            BugId = bugId,
            TesterId = userId,
            Result = result,
            ActualResult = NormalizeOptional(request.ActualResult),
            Evidence = NormalizeOptional(request.Evidence),
            CreatedBugId = createdBugId,
            ExecutedAt = DateTime.UtcNow
        };

        await _testExecutionDomain.CreateAsync(entity);
        await EnsureProjectMemberAsync(plan.ProjectId, userId, SprintProjectMemberRoles.Tester);
        await BindCreatedBugForExecutionAsync(plan, entity);
        await ReopenBugForFailedExecutionAsync(plan, entity);
        if (result == TestExecutionResults.Passed)
        {
            var requirement = await _requirementDomain.GetAsync(plan.RequirementId) ??
                throw new InvalidOperationException("Requirement does not exist.");
            requirement.Status = SprintRequirementStatuses.Tested;
            requirement.TestedAt = DateTime.UtcNow;
            await _requirementDomain.UpdateAsync(requirement);
        }

        plan.Status = result switch
        {
            TestExecutionResults.Passed => TestPlanStatuses.Passed,
            TestExecutionResults.Failed => TestPlanStatuses.Failed,
            TestExecutionResults.Blocked => TestPlanStatuses.Blocked,
            _ => plan.Status
        };
        plan.CompletedAt = result == TestExecutionResults.Blocked ? null : DateTime.UtcNow;
        await _testPlanDomain.UpdateAsync(plan);

        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TestExecutionResult>> ListExecutionsAsync(string testPlanId)
    {
        var entities = await _testExecutionDomain.ListAsync(entity => entity.TestPlanId == testPlanId);
        return entities
            .OrderByDescending(entity => entity.ExecutedAt)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<TestExecutionResult> UpdateExecutionBugAsync(
        string testPlanId,
        string executionId,
        UpdateTestExecutionBugRequest request)
    {
        var entity = await _testExecutionDomain.GetAsync(executionId) ??
            throw new InvalidOperationException("Test execution does not exist.");
        if (!string.Equals(entity.TestPlanId, testPlanId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Test execution does not belong to this plan.");
        }

        entity.BugId = NormalizeOptional(request.BugId);
        entity.CreatedBugId = NormalizeOptional(request.CreatedBugId);
        await _testExecutionDomain.UpdateAsync(entity);
        var plan = await GetPlanOrThrowAsync(testPlanId);
        await BindCreatedBugForExecutionAsync(plan, entity);
        await ReopenBugForFailedExecutionAsync(plan, entity);
        return ToResult(entity);
    }

    private async Task BindCreatedBugForExecutionAsync(TestPlanEntity plan, TestExecutionEntity execution)
    {
        if (string.IsNullOrWhiteSpace(execution.CreatedBugId))
        {
            return;
        }

        var bug = await _bugDomain.GetAsync(execution.CreatedBugId) ??
            throw new InvalidOperationException("Created bug does not exist.");
        if (!string.Equals(bug.ProjectId, plan.ProjectId, StringComparison.Ordinal) ||
            !string.Equals(bug.RequirementId, plan.RequirementId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Created bug does not belong to this test plan requirement.");
        }

        bug.TestPlanId = plan.Id;
        bug.TestExecutionId = execution.Id;
        await _bugDomain.UpdateAsync(bug);

        if (execution.Result == TestExecutionResults.Failed)
        {
            var requirement = await _requirementDomain.GetAsync(plan.RequirementId) ??
                throw new InvalidOperationException("Requirement does not exist.");
            requirement.Status = SprintRequirementStatuses.PendingFix;
            await _requirementDomain.UpdateAsync(requirement);
        }
    }

    private async Task ReopenBugForFailedExecutionAsync(TestPlanEntity plan, TestExecutionEntity execution)
    {
        if (execution.Result != TestExecutionResults.Failed || string.IsNullOrWhiteSpace(execution.BugId))
        {
            return;
        }

        var bug = await _bugDomain.GetAsync(execution.BugId) ??
            throw new InvalidOperationException("Linked bug does not exist.");
        EnsureBugBelongsToPlanRequirement(
            bug,
            plan.ProjectId,
            plan.RequirementId,
            "Linked bug");

        bug.Status = SprintBugStatuses.Open;
        bug.FixedAt = null;
        await _bugDomain.UpdateAsync(bug);

        var requirement = await _requirementDomain.GetAsync(plan.RequirementId) ??
            throw new InvalidOperationException("Requirement does not exist.");
        requirement.Status = SprintRequirementStatuses.PendingFix;
        await _requirementDomain.UpdateAsync(requirement);
    }

    private async Task<TestPlanEntity> GetPlanOrThrowAsync(string id)
    {
        return await _testPlanDomain.GetAsync(id) ??
            throw new InvalidOperationException("Test plan does not exist.");
    }

    private static void EnsureTestableRequirementStatus(SprintRequirementEntity requirement)
    {
        if (!TestableRequirementStatuses.Contains(requirement.Status))
        {
            throw new InvalidOperationException("Requirement is not ready for testing.");
        }
    }

    private async Task EnsureBugBelongsToPlanRequirementAsync(
        string bugId,
        string projectId,
        string requirementId,
        string messagePrefix)
    {
        var bug = await _bugDomain.GetAsync(bugId) ??
            throw new InvalidOperationException($"{messagePrefix} does not exist.");
        EnsureBugBelongsToPlanRequirement(bug, projectId, requirementId, messagePrefix);
    }

    private static void EnsureBugBelongsToPlanRequirement(
        SprintBugEntity bug,
        string projectId,
        string requirementId,
        string messagePrefix)
    {
        if (!string.Equals(bug.ProjectId, projectId, StringComparison.Ordinal) ||
            !string.Equals(bug.RequirementId, requirementId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{messagePrefix} does not belong to this test plan requirement.");
        }
    }

    private async Task EnsureProjectMemberAsync(string projectId, string userId, string role)
    {
        if (string.IsNullOrWhiteSpace(projectId) ||
            string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        var normalizedUserId = userId.Trim();
        var normalizedRole = role.Trim();
        var existing = await _projectMemberDomain.ListAsync(entity =>
            entity.ProjectId == projectId &&
            entity.UserId == normalizedUserId &&
            entity.Role == normalizedRole);
        if (existing.Count > 0)
        {
            var member = existing[0];
            if (member.Status != SprintProjectMemberStatuses.Active)
            {
                member.Status = SprintProjectMemberStatuses.Active;
                await _projectMemberDomain.UpdateAsync(member);
            }

            return;
        }

        await _projectMemberDomain.CreateAsync(new SprintProjectMemberEntity
        {
            ProjectId = projectId,
            UserId = normalizedUserId,
            Role = normalizedRole
        });
    }

    private static string NormalizeStatus(string status)
    {
        var normalized = status.Trim().ToLowerInvariant();
        if (!ValidPlanStatuses.Contains(normalized))
        {
            throw new InvalidOperationException("Invalid test plan status.");
        }

        return normalized;
    }

    private static string NormalizeExecutionResult(string result)
    {
        var normalized = result.Trim().ToLowerInvariant();
        if (!ValidExecutionResults.Contains(normalized))
        {
            throw new InvalidOperationException("Invalid test execution result.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static TestPlanResult ToResult(TestPlanEntity entity)
    {
        return new TestPlanResult(
            entity.Id,
            entity.ProjectId,
            entity.RequirementId,
            entity.BugId,
            entity.Name,
            entity.Environment,
            entity.TestUrl,
            entity.Status,
            entity.CreatedBy,
            entity.StartedAt,
            entity.CompletedAt,
            entity.Summary,
            entity.CreateTime,
            entity.TesterId);
    }

    private static TestExecutionResult ToResult(TestExecutionEntity entity)
    {
        return new TestExecutionResult(
            entity.Id,
            entity.TestPlanId,
            entity.RequirementId,
            entity.BugId,
            entity.TesterId,
            entity.Result,
            entity.ActualResult,
            entity.Evidence,
            entity.CreatedBugId,
            entity.ExecutedAt,
            entity.CreateTime);
    }
}
