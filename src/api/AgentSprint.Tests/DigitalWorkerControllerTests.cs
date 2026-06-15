using System.Security.Claims;

using AgentSprint.Entry;
using AgentSprint.Entry.Controllers;
using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Tests;

public sealed class DigitalWorkerControllerTests
{
    [Fact]
    public async Task Management_CreateWorker_UsesAuthenticatedUser()
    {
        var service = new CapturingDigitalWorkerManagementService();
        var controller = CreateManagementController(service, "admin-1");

        var result = await controller.CreateWorker(
            new CreateDigitalWorkerRequest("Worker 1", "agent-1", Code: "worker-1"));

        var response = Assert.IsType<ApiResponse<DigitalWorkerResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("admin-1", service.LastUserId);
        Assert.Equal("worker-1", response.Data?.Code);
    }

    [Fact]
    public async Task Management_CreateCommand_UsesAuthenticatedUser()
    {
        var service = new CapturingDigitalWorkerManagementService();
        var controller = CreateManagementController(service, "admin-2");

        var result = await controller.CreateCommand(
            new CreateWorkerCommandRequest("worker-id", WorkerCommandTypes.Smoke));

        var response = Assert.IsType<ApiResponse<WorkerCommandResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("admin-2", service.LastUserId);
        Assert.Equal(WorkerCommandTypes.Smoke, response.Data?.CommandType);
    }

    [Fact]
    public async Task Runtime_RegisterSession_ForwardsPayload()
    {
        var service = new CapturingDigitalWorkerRuntimeService();
        var controller = new DigitalWorkerRuntimeController(service);

        var result = await controller.RegisterSession(
            new RegisterWorkerSessionRequest("worker-id", "instance-1", HostName: "host-1"));

        var response = Assert.IsType<ApiResponse<WorkerSessionResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("worker-id", service.LastRegisterRequest?.WorkerId);
        Assert.Equal("instance-1", response.Data?.InstanceId);
    }

    [Fact]
    public async Task Runtime_Heartbeat_ReturnsCommands()
    {
        var service = new CapturingDigitalWorkerRuntimeService();
        var controller = new DigitalWorkerRuntimeController(service);

        var result = await controller.Heartbeat(
            new WorkerHeartbeatRequest("worker-id", "session-id", WorkerSessionStatuses.Idle));

        var response = Assert.IsType<ApiResponse<WorkerHeartbeatResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Single(response.Data!.Commands);
        Assert.Equal("session-id", service.LastHeartbeatRequest?.SessionId);
    }

    [Fact]
    public async Task Runtime_GetRuntimeConfig_UsesBearerAgentToken()
    {
        var service = new CapturingDigitalWorkerRuntimeService();
        var controller = new DigitalWorkerRuntimeController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Headers.Authorization = "Bearer agent-token-value";

        var result = await controller.GetRuntimeConfig();

        var response = Assert.IsType<ApiResponse<WorkerRuntimeConfigResult>>(result.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("agent-token-value", service.LastAgentToken);
        Assert.Equal("worker-id-from-token", response.Data?.WorkerId);
    }

    private static DigitalWorkerManagementController CreateManagementController(
        IDigitalWorkerManagementService service,
        string userId)
    {
        return new DigitalWorkerManagementController(service)
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

internal sealed class CapturingDigitalWorkerManagementService : IDigitalWorkerManagementService
{
    public string? LastUserId { get; private set; }

    public Task<DigitalWorkerResult> CreateWorkerAsync(CreateDigitalWorkerRequest request, string userId)
    {
        LastUserId = userId;
        return Task.FromResult(CreateWorkerResult(
            "worker-id",
            request.Code,
            request.Name,
            request.AgentUserId,
            request.AgentTokenId,
            request.ProjectIds ?? [],
            request.EndpointIds ?? [],
            request.SkillIds ?? [],
            request.EmployeeType ?? DigitalWorkerEmployeeTypes.Development,
            request.WorkerType ?? DigitalWorkerTypes.Codex,
            DigitalWorkerStatuses.Active,
            request.MaxConcurrentRuns ?? 1,
            request.HeartbeatTimeoutSeconds ?? 90,
            request.Description,
            userId));
    }

    public Task<DigitalWorkerResult> UpdateWorkerAsync(string id, UpdateDigitalWorkerRequest request)
    {
        return Task.FromResult(CreateWorkerResult(
            id,
            "worker-code",
            request.Name,
            request.AgentUserId,
            request.AgentTokenId,
            request.ProjectIds ?? [],
            request.EndpointIds ?? [],
            request.SkillIds ?? [],
            request.EmployeeType ?? DigitalWorkerEmployeeTypes.Development,
            DigitalWorkerTypes.Codex,
            request.Status ?? DigitalWorkerStatuses.Active,
            request.MaxConcurrentRuns ?? 1,
            request.HeartbeatTimeoutSeconds ?? 90,
            request.Description,
            "admin",
            DateTime.UtcNow));
    }

    public Task<IReadOnlyList<DigitalWorkerResult>> ListWorkersAsync(
        string? status = null,
        string? workerType = null,
        string? keyword = null)
    {
        IReadOnlyList<DigitalWorkerResult> workers = [];
        return Task.FromResult(workers);
    }

    public Task<DigitalWorkerDetailResult> GetWorkerDetailAsync(string id)
    {
        return Task.FromResult(new DigitalWorkerDetailResult(
            CreateWorkerResult(
                id,
                "worker-code",
                "Worker",
                "agent-1",
                null,
                [],
                [],
                [],
                DigitalWorkerEmployeeTypes.Development,
                DigitalWorkerTypes.Codex,
                DigitalWorkerStatuses.Active,
                1,
                90,
                null,
                "admin"),
            null,
            null,
            []));
    }

    public Task<DigitalWorkerResult> SetWorkerStatusAsync(string id, SetDigitalWorkerStatusRequest request)
    {
        return Task.FromResult(CreateWorkerResult(
            id,
            "worker-code",
            "Worker",
            "agent-1",
            null,
            [],
            [],
            [],
            DigitalWorkerEmployeeTypes.Development,
            DigitalWorkerTypes.Codex,
            request.Status,
            1,
            90,
            null,
            "admin",
            DateTime.UtcNow));
    }

    public Task<WorkerCommandResult> CreateCommandAsync(CreateWorkerCommandRequest request, string userId)
    {
        LastUserId = userId;
        return Task.FromResult(new WorkerCommandResult(
            "command-id",
            request.WorkerId,
            request.SessionId,
            request.CommandType,
            request.PayloadJson,
            WorkerCommandStatuses.Pending,
            null,
            null,
            null,
            request.ExpiresAt,
            null,
            null,
            userId,
            DateTime.UtcNow));
    }

    public Task<IReadOnlyList<WorkerSessionResult>> ListSessionsAsync(string? workerId = null, string? status = null)
    {
        IReadOnlyList<WorkerSessionResult> sessions = [];
        return Task.FromResult(sessions);
    }

    public Task<IReadOnlyList<WorkerRunResult>> ListRunsAsync(
        string? workerId = null,
        string? sessionId = null,
        string? targetType = null,
        string? targetId = null,
        string? status = null)
    {
        IReadOnlyList<WorkerRunResult> runs = [];
        return Task.FromResult(runs);
    }

    public Task<IReadOnlyList<WorkerEventResult>> ListEventsAsync(
        string? workerId = null,
        string? sessionId = null,
        string? runId = null,
        string? eventType = null)
    {
        IReadOnlyList<WorkerEventResult> events = [];
        return Task.FromResult(events);
    }

    private static DigitalWorkerResult CreateWorkerResult(
        string id,
        string? code,
        string name,
        string agentUserId,
        string? agentTokenId,
        IReadOnlyList<string> projectIds,
        IReadOnlyList<string> endpointIds,
        IReadOnlyList<string> skillIds,
        string employeeType,
        string workerType,
        string status,
        int maxConcurrentRuns,
        int heartbeatTimeoutSeconds,
        string? description,
        string createdBy,
        DateTime? updateTime = null)
    {
        return new DigitalWorkerResult(
            id,
            code ?? "worker-code",
            name,
            agentUserId,
            agentTokenId,
            projectIds,
            endpointIds,
            skillIds,
            employeeType,
            workerType,
            status,
            maxConcurrentRuns,
            heartbeatTimeoutSeconds,
            15,
            180,
            60,
            "/workspaces",
            "/runs",
            "/codex-home",
            "workspace-write",
            false,
            "你好",
            "openai",
            "gpt-5.4",
            null,
            1,
            description,
            createdBy,
            DateTime.UtcNow,
            updateTime);
    }
}

internal sealed class CapturingDigitalWorkerRuntimeService : IDigitalWorkerRuntimeService
{
    public string? LastAgentToken { get; private set; }

    public RegisterWorkerSessionRequest? LastRegisterRequest { get; private set; }

    public WorkerHeartbeatRequest? LastHeartbeatRequest { get; private set; }

    public Task<WorkerRuntimeConfigResult> GetRuntimeConfigAsync(string workerId)
    {
        return Task.FromResult(new WorkerRuntimeConfigResult(
            workerId,
            "worker-code",
            "Worker",
            null,
            null,
            "/workspaces",
            "/runs",
            "/codex-home",
            15,
            180,
            60,
            "workspace-write",
            false,
            "你好",
            "openai",
            "gpt-5.4",
            null,
            "agent-token",
            1));
    }

    public Task<WorkerRuntimeConfigResult> GetRuntimeConfigByAgentTokenAsync(string agentToken)
    {
        LastAgentToken = agentToken;
        return Task.FromResult(new WorkerRuntimeConfigResult(
            "worker-id-from-token",
            "worker-code",
            "Worker",
            null,
            null,
            "/workspaces",
            "/runs",
            "/codex-home",
            15,
            180,
            60,
            "workspace-write",
            false,
            "浣犲ソ",
            "openai",
            "gpt-5.4",
            null,
            agentToken,
            1));
    }

    public Task<WorkerSessionResult> RegisterSessionAsync(RegisterWorkerSessionRequest request)
    {
        LastRegisterRequest = request;
        return Task.FromResult(new WorkerSessionResult(
            "session-id",
            request.WorkerId,
            request.InstanceId,
            request.HostName,
            request.ContainerId,
            WorkerSessionStatuses.Idle,
            request.CodexVersion,
            request.GitVersion,
            request.DotnetVersion,
            request.NodeVersion,
            request.ConfigTomlExists,
            request.CodexHome,
            request.WorkspaceRoot,
            request.RunsRoot,
            null,
            request.ErrorSummary,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null));
    }

    public Task<WorkerHeartbeatResult> HeartbeatAsync(WorkerHeartbeatRequest request)
    {
        LastHeartbeatRequest = request;
        return Task.FromResult(new WorkerHeartbeatResult(
            request.WorkerId,
            request.SessionId,
            request.Status,
            15,
            [
                new WorkerCommandResult(
                    "command-id",
                    request.WorkerId,
                    request.SessionId,
                    WorkerCommandTypes.Smoke,
                    null,
                    WorkerCommandStatuses.Pending,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "admin",
                    DateTime.UtcNow)
            ]));
    }

    public Task<WorkerCommandResult> AckCommandAsync(string commandId, AckWorkerCommandRequest request)
    {
        return CreateCommandResult(commandId, request.SessionId, WorkerCommandStatuses.Acked);
    }

    public Task<WorkerPromptResult> GetWorkPromptAsync(string workerId, string targetType, string targetId)
    {
        return Task.FromResult(new WorkerPromptResult(
            targetType,
            targetId,
            "digital_worker_task_execution",
            "数字员工任务执行提示词",
            "Worker prompt",
            new WorkerPromptContextResult(
                targetType,
                targetId,
                "project-id",
                "project-code",
                "Project",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "requirement-id",
                "Requirement",
                null,
                null,
                null,
                null,
                targetType == WorkerRunTypes.Task ? targetId : null,
                null,
                null,
                targetType == WorkerRunTypes.Bug ? targetId : null,
                null,
                null,
                null,
                null,
                null,
                "/worker-runtime/work/task/target/complete",
                "Worker completes through API.")));
    }

    public Task<WorkerWorkCompletionResult> CompleteWorkAsync(string workerId, string targetType, string targetId)
    {
        return Task.FromResult(new WorkerWorkCompletionResult(targetType, targetId, "completed"));
    }

    public Task<WorkerCommandResult> StartCommandAsync(string commandId, AckWorkerCommandRequest request)
    {
        return CreateCommandResult(commandId, request.SessionId, WorkerCommandStatuses.Running);
    }

    public Task<WorkerRunResult> StartRunAsync(StartWorkerRunRequest request)
    {
        return Task.FromResult(new WorkerRunResult(
            "run-id",
            request.WorkerId,
            request.SessionId,
            request.CommandId,
            request.RunType,
            request.TargetType,
            request.TargetId,
            request.Status,
            request.WorkspacePath,
            request.PromptPath,
            request.StdoutPath,
            request.StderrPath,
            request.FinalPath,
            request.ManifestPath,
            null,
            false,
            null,
            DateTime.UtcNow,
            null));
    }

    public Task<WorkerRunResult> FinishRunAsync(string runId, FinishWorkerRunRequest request)
    {
        return Task.FromResult(new WorkerRunResult(
            runId,
            "worker-id",
            "session-id",
            null,
            WorkerRunTypes.Smoke,
            null,
            null,
            request.Status,
            null,
            null,
            null,
            null,
            null,
            null,
            request.ExitCode,
            request.TimedOut,
            request.Error,
            DateTime.UtcNow,
            DateTime.UtcNow));
    }

    public Task<WorkerEventResult> ReportEventAsync(ReportWorkerEventRequest request)
    {
        return Task.FromResult(new WorkerEventResult(
            "event-id",
            request.WorkerId,
            request.SessionId,
            request.RunId,
            request.EventType,
            request.Level ?? WorkerEventLevels.Info,
            request.Message,
            request.PayloadJson,
            DateTime.UtcNow));
    }

    private static Task<WorkerCommandResult> CreateCommandResult(string id, string sessionId, string status)
    {
        return Task.FromResult(new WorkerCommandResult(
            id,
            "worker-id",
            sessionId,
            WorkerCommandTypes.Smoke,
            null,
            status,
            DateTime.UtcNow,
            status == WorkerCommandStatuses.Running ? DateTime.UtcNow : null,
            null,
            null,
            null,
            null,
            "admin",
            DateTime.UtcNow));
    }
}
