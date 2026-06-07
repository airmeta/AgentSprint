using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using AgentSprint.Model.Modules.Common;

namespace AgentSprint.Model.Modules.Tests;

[Table("test_plan")]
public sealed class TestPlanEntity : EntityBase
{
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RequirementId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? BugId { get; set; }

    [MaxLength(64)]
    public string? TesterId { get; set; }

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Environment { get; set; } = "test";

    [MaxLength(512)]
    public string? TestUrl { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = TestPlanStatuses.Pending;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [MaxLength(1024)]
    public string? Summary { get; set; }
}

[Table("test_execution")]
public sealed class TestExecutionEntity : EntityBase
{
    [MaxLength(64)]
    public string TestPlanId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RequirementId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? BugId { get; set; }

    [MaxLength(64)]
    public string TesterId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Result { get; set; } = TestExecutionResults.Passed;

    [MaxLength(2048)]
    public string? ActualResult { get; set; }

    [MaxLength(2048)]
    public string? Evidence { get; set; }

    [MaxLength(64)]
    public string? CreatedBugId { get; set; }

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

public static class TestPlanStatuses
{
    public const string Pending = "pending";

    public const string Testing = "testing";

    public const string Passed = "passed";

    public const string Failed = "failed";

    public const string Blocked = "blocked";

    public const string Closed = "closed";
}

public static class TestExecutionResults
{
    public const string Passed = "passed";

    public const string Failed = "failed";

    public const string Blocked = "blocked";
}
