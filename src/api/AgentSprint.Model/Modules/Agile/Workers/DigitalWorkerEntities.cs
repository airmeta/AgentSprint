using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using AgentSprint.Model.Modules.Common;

namespace AgentSprint.Model.Modules.Agile.Workers;

[Table("digital_worker")]
public sealed class DigitalWorkerEntity : EntityBase
{
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string AgentUserId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? AgentTokenId { get; set; }

    [MaxLength(1024)]
    public string? ProjectIds { get; set; }

    [MaxLength(1024)]
    public string? EndpointIds { get; set; }

    [MaxLength(1024)]
    public string? SkillIds { get; set; }

    [MaxLength(32)]
    public string EmployeeType { get; set; } = DigitalWorkerEmployeeTypes.Development;

    [MaxLength(32)]
    public string WorkerType { get; set; } = DigitalWorkerTypes.Codex;

    [MaxLength(32)]
    public string Status { get; set; } = DigitalWorkerStatuses.Active;

    public int MaxConcurrentRuns { get; set; } = 1;

    public int HeartbeatTimeoutSeconds { get; set; } = 90;

    public int PollIntervalSeconds { get; set; } = 15;

    public int IdleMaxIntervalSeconds { get; set; } = 180;

    public int MaxRunMinutes { get; set; } = 60;

    [MaxLength(512)]
    public string WorkspaceRoot { get; set; } = "/workspaces";

    [MaxLength(512)]
    public string RunsRoot { get; set; } = "/runs";

    [MaxLength(512)]
    public string CodexHome { get; set; } = "/codex-home";

    [MaxLength(32)]
    public string SandboxMode { get; set; } = "workspace-write";

    public bool RunSmokeOnStartup { get; set; }

    [MaxLength(1024)]
    public string? SmokePrompt { get; set; }

    [MaxLength(64)]
    public string CodexProvider { get; set; } = "openai";

    [MaxLength(128)]
    public string CodexModel { get; set; } = "gpt-5.4";

    [MaxLength(512)]
    public string? OpenAiBaseUrl { get; set; }

    public int ConfigVersion { get; set; } = 1;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("worker_session")]
public sealed class WorkerSessionEntity : EntityBase
{
    [MaxLength(64)]
    public string WorkerId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string InstanceId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? HostName { get; set; }

    [MaxLength(128)]
    public string? ContainerId { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = WorkerSessionStatuses.Starting;

    [MaxLength(128)]
    public string? CodexVersion { get; set; }

    [MaxLength(128)]
    public string? GitVersion { get; set; }

    [MaxLength(128)]
    public string? DotnetVersion { get; set; }

    [MaxLength(128)]
    public string? NodeVersion { get; set; }

    public bool ConfigTomlExists { get; set; }

    [MaxLength(512)]
    public string? CodexHome { get; set; }

    [MaxLength(512)]
    public string? WorkspaceRoot { get; set; }

    [MaxLength(512)]
    public string? RunsRoot { get; set; }

    [MaxLength(64)]
    public string? CurrentRunId { get; set; }

    [MaxLength(1024)]
    public string? ErrorSummary { get; set; }

    public DateTime? LastHeartbeatAt { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StoppedAt { get; set; }
}

[Table("worker_command")]
public sealed class WorkerCommandEntity : EntityBase
{
    [MaxLength(64)]
    public string WorkerId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? SessionId { get; set; }

    [MaxLength(32)]
    public string CommandType { get; set; } = WorkerCommandTypes.Smoke;

    [Column(TypeName = "text")]
    public string? PayloadJson { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = WorkerCommandStatuses.Pending;

    public DateTime? AckedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    [Column(TypeName = "text")]
    public string? ResultJson { get; set; }

    [MaxLength(1024)]
    public string? Error { get; set; }

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;
}

[Table("worker_run")]
public sealed class WorkerRunEntity : EntityBase
{
    [MaxLength(64)]
    public string WorkerId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string SessionId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? CommandId { get; set; }

    [MaxLength(32)]
    public string RunType { get; set; } = WorkerRunTypes.Command;

    [MaxLength(32)]
    public string? TargetType { get; set; }

    [MaxLength(64)]
    public string? TargetId { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = WorkerRunStatuses.Pending;

    [MaxLength(512)]
    public string? WorkspacePath { get; set; }

    [MaxLength(512)]
    public string? PromptPath { get; set; }

    [MaxLength(512)]
    public string? StdoutPath { get; set; }

    [MaxLength(512)]
    public string? StderrPath { get; set; }

    [MaxLength(512)]
    public string? FinalPath { get; set; }

    [MaxLength(512)]
    public string? ManifestPath { get; set; }

    public int? ExitCode { get; set; }

    public bool TimedOut { get; set; }

    [MaxLength(1024)]
    public string? Error { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}

[Table("worker_event")]
public sealed class WorkerEventEntity : EntityBase
{
    [MaxLength(64)]
    public string WorkerId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? SessionId { get; set; }

    [MaxLength(64)]
    public string? RunId { get; set; }

    [MaxLength(64)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(16)]
    public string Level { get; set; } = WorkerEventLevels.Info;

    [MaxLength(1024)]
    public string Message { get; set; } = string.Empty;

    [Column(TypeName = "text")]
    public string? PayloadJson { get; set; }
}
