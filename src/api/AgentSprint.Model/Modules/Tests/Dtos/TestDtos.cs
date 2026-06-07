namespace AgentSprint.Model.Modules.Tests.Dtos;

public sealed record CreateTestPlanRequest(
    string ProjectId,
    string RequirementId,
    string Name,
    string? Environment,
    string? TestUrl,
    string? BugId,
    string? TesterId = null);

public sealed record CompleteTestPlanRequest(string Status, string? Summary);

public sealed record SubmitTestExecutionRequest(
    string Result,
    string? ActualResult,
    string? Evidence,
    string? BugId,
    string? CreatedBugId);

public sealed record UpdateTestExecutionBugRequest(string? BugId, string? CreatedBugId);

public sealed record TestPlanResult(
    string Id,
    string ProjectId,
    string RequirementId,
    string? BugId,
    string Name,
    string Environment,
    string? TestUrl,
    string Status,
    string CreatedBy,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? Summary,
    DateTime CreateTime,
    string? TesterId = null);

public sealed record TestExecutionResult(
    string Id,
    string TestPlanId,
    string RequirementId,
    string? BugId,
    string TesterId,
    string Result,
    string? ActualResult,
    string? Evidence,
    string? CreatedBugId,
    DateTime ExecutedAt,
    DateTime CreateTime);
