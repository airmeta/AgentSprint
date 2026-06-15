using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AgentSprint.Worker.Actors;
using AgentSprint.Worker.Models;
using AgentSprint.Worker.Options;

using Air.Cloud.Core;
using Air.Cloud.Modules.Akka.Abstractions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AgentSprint.Worker.Services;

public sealed class AgentSprintWorkerService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentSprintApiClient _apiClient;
    private readonly AgentSprintOptions _agentSprintOptions;
    private readonly IAkkaClusterService _akkaClusterService;
    private readonly CodexProcessRunner _codexProcessRunner;
    private readonly WorkerEnvironmentProbe _environmentProbe;
    private readonly GitWorkspaceManager _gitWorkspaceManager;
    private readonly WorkerOptions _options;
    private readonly WorkerRuntimeConfigApplier _runtimeConfigApplier;
    private readonly WorkerRunLogger _runLogger;

    /// <summary>
    /// <para>zh-cn:创建数字员工受控端后台服务。该服务是 HostApp 控制台宿主中的主循环，负责启动探针、可选 smoke run，以及后续接入平台心跳和任务领取的轮询节奏。</para>
    /// <para>en-us:Creates the digital-worker controlled background service. This service is the main loop inside the HostApp console host and is responsible for startup probing, optional smoke runs, and the polling cadence used later for platform heartbeat and work claiming.</para>
    /// </summary>
    /// <param name="environmentProbe">
    /// <para>zh-cn:运行环境探针。</para>
    /// <para>en-us:Runtime environment probe.</para>
    /// </param>
    /// <param name="codexProcessRunner">
    /// <para>zh-cn:Codex CLI 执行器。</para>
    /// <para>en-us:Codex CLI runner.</para>
    /// </param>
    /// <param name="apiClient">
    /// <para>zh-cn:AgentSprint 主平台客户端。</para>
    /// <para>en-us:AgentSprint platform client.</para>
    /// </param>
    /// <param name="akkaClusterService">
    /// <para>zh-cn:Akka.Cluster 消息入口，用于把 Worker 事件投递给事件上报 Actor。</para>
    /// <para>en-us:Akka.Cluster message entry used to enqueue Worker events into the event reporting actor.</para>
    /// </param>
    /// <param name="options">
    /// <para>zh-cn:Worker 运行配置。</para>
    /// <para>en-us:Worker runtime options.</para>
    /// </param>
    public AgentSprintWorkerService(
        WorkerEnvironmentProbe environmentProbe,
        CodexProcessRunner codexProcessRunner,
        GitWorkspaceManager gitWorkspaceManager,
        AgentSprintApiClient apiClient,
        IAkkaClusterService akkaClusterService,
        WorkerRuntimeConfigApplier runtimeConfigApplier,
        WorkerRunLogger runLogger,
        IOptions<AgentSprintOptions> agentSprintOptions,
        IOptions<WorkerOptions> options)
    {
        _environmentProbe = environmentProbe;
        _codexProcessRunner = codexProcessRunner;
        _gitWorkspaceManager = gitWorkspaceManager;
        _apiClient = apiClient;
        _akkaClusterService = akkaClusterService;
        _runtimeConfigApplier = runtimeConfigApplier;
        _runLogger = runLogger;
        _agentSprintOptions = agentSprintOptions.Value;
        _options = options.Value;
    }

    /// <summary>
    /// <para>zh-cn:执行 Worker 主循环。启动后会先运行环境探针；若开启 RunSmokeOnStartup 且 codex 可用，会执行一次本地 smoke run；之后按 PollIntervalSeconds 周期保持轮询，为后续平台心跳和命令处理提供稳定生命周期。</para>
    /// <para>en-us:Runs the Worker main loop. Startup first performs the environment probe; when RunSmokeOnStartup is enabled and codex is available, it runs one local smoke run; then it keeps polling by PollIntervalSeconds to provide a stable lifecycle for later platform heartbeat and command handling.</para>
    /// </summary>
    /// <param name="stoppingToken">
    /// <para>zh-cn:宿主停止令牌。</para>
    /// <para>en-us:Host stopping token.</para>
    /// </param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            AppRealization.TraceLog.Write(
                AppRealization.JSON.Serialize(new
                {
                    level = "Information",
                    message = "AgentSprint worker is starting.",
                    workerId = _options.WorkerId,
                    workerName = _options.WorkerName
                }),
                new Dictionary<string, string>()
                {
                    { "workerId", _options.WorkerId },
                    { "workerName", _options.WorkerName }
                });
        }
        catch
        {
        }

        await _apiClient.ProbeAsync(stoppingToken);
        if (_agentSprintOptions.PullRuntimeConfigOnStartup)
        {
            var config = await _apiClient.GetRuntimeConfigAsync(stoppingToken);
            await _runtimeConfigApplier.ApplyAsync(config, stoppingToken);
            _apiClient.UseAgentToken(config.AgentToken);
        }

        var snapshot = await _environmentProbe.ProbeAsync(stoppingToken);
        var session = await RegisterSessionAsync(snapshot, stoppingToken);
        await ReportEventAsync(session.Id, null, "worker_probe_finished", "info", "Worker environment probe finished.", stoppingToken);
        await ReportAkkaClusterStartedAsync(session.Id, stoppingToken);

        if (!snapshot.CanEnterWorkLoop)
        {
            try
            {
                AppRealization.TraceLog.Write(
                    AppRealization.JSON.Serialize(new
                    {
                        level = "Error",
                        message = "Codex CLI is unavailable. Worker will stay alive but will not execute work.",
                        workerId = _options.WorkerId
                    }),
                    new Dictionary<string, string>()
                    {
                        { "workerId", _options.WorkerId }
                    });
            }
            catch
            {
            }
        }
        else if (!snapshot.IsCodexAuthenticated)
        {
            try
            {
                AppRealization.TraceLog.Write(
                    AppRealization.JSON.Serialize(new
                    {
                        level = "Warning",
                        message = "Codex login status is not healthy. Worker is in auth_required mode.",
                        workerId = _options.WorkerId
                    }),
                    new Dictionary<string, string>()
                    {
                        { "workerId", _options.WorkerId }
                    });
            }
            catch
            {
            }
        }

        if (_options.RunSmokeOnStartup && snapshot.CanEnterWorkLoop)
        {
            await RunSmokeAsync(snapshot, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var heartbeatStatus = ResolveHeartbeatStatus(snapshot);
            var heartbeat = await _apiClient.HeartbeatAsync(
                new WorkerHeartbeatRequest(
                    _options.WorkerId,
                    session.Id,
                    heartbeatStatus,
                    CurrentRunId: null,
                    ErrorSummary: heartbeatStatus == WorkerPlatformStatuses.Error
                        ? "Codex CLI is unavailable."
                        : null),
                stoppingToken);

            foreach (var command in heartbeat.Commands)
            {
                await HandleCommandAsync(command, session.Id, snapshot, stoppingToken);
            }

            var delaySeconds = heartbeat.NextIntervalSeconds > 0
                ? heartbeat.NextIntervalSeconds
                : _options.PollIntervalSeconds;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }

    private async Task<WorkerSessionResult> RegisterSessionAsync(
        WorkerEnvironmentSnapshot snapshot,
        CancellationToken stoppingToken)
    {
        var request = new RegisterWorkerSessionRequest(
            _options.WorkerId,
            InstanceId: $"{Environment.MachineName}-{Guid.NewGuid():N}",
            HostName: Environment.MachineName,
            ContainerId: Environment.GetEnvironmentVariable("HOSTNAME"),
            CodexVersion: ResolveProbeOutput(snapshot.CodexVersion),
            GitVersion: ResolveProbeOutput(snapshot.GitVersion),
            DotnetVersion: ResolveProbeOutput(snapshot.DotnetVersion),
            NodeVersion: ResolveProbeOutput(snapshot.NodeVersion),
            ConfigTomlExists: snapshot.ConfigTomlExists,
            CodexHome: snapshot.CodexHome,
            WorkspaceRoot: snapshot.WorkspaceRoot,
            RunsRoot: snapshot.RunsRoot,
            ErrorSummary: snapshot.CanEnterWorkLoop ? null : "Codex CLI is unavailable.");

        return await _apiClient.RegisterSessionAsync(request, stoppingToken);
    }

    private async Task HandleCommandAsync(
        WorkerCommandResult command,
        string sessionId,
        WorkerEnvironmentSnapshot snapshot,
        CancellationToken stoppingToken)
    {
        await _apiClient.AckCommandAsync(command.Id, new AckWorkerCommandRequest(sessionId), stoppingToken);

        if (command.CommandType == WorkerPlatformCommandTypes.Smoke)
        {
            await RunPlatformSmokeAsync(command, sessionId, snapshot, stoppingToken);
            return;
        }

        if (command.CommandType is WorkerPlatformCommandTypes.StartTask or WorkerPlatformCommandTypes.StartBug)
        {
            await RunAssignedWorkAsync(command, sessionId, snapshot, stoppingToken);
            return;
        }

        try
        {
            AppRealization.TraceLog.Write(
                AppRealization.JSON.Serialize(new
                {
                    level = "Warning",
                    message = "Unsupported worker command.",
                    commandId = command.Id,
                    commandType = command.CommandType
                }),
                new Dictionary<string, string>()
                {
                    { "commandId", command.Id },
                    { "commandType", command.CommandType }
                });
        }
        catch
        {
        }

        await FinishFailedRunWithoutCodexAsync(
            command,
            sessionId,
            WorkerPlatformStatuses.CodexFailed,
            "command",
            null,
            null,
            null,
            $"Unsupported worker command type: {command.CommandType}.",
            stoppingToken);
    }

    private async Task RunPlatformSmokeAsync(
        WorkerCommandResult command,
        string sessionId,
        WorkerEnvironmentSnapshot snapshot,
        CancellationToken stoppingToken)
    {
        var runId = "smoke-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var workspace = ResolveSmokeWorkspace(snapshot.WorkspaceRoot);
        var request = new CodexRunRequest(
            runId,
            workspace,
            _options.SmokePrompt,
            _options.SandboxMode,
            SkipGitRepoCheck: true,
            TimeSpan.FromMinutes(_options.MaxRunMinutes),
            TimeSpan.FromSeconds(_options.CodexIdleTimeoutSeconds),
            _options.CodexExecutable);
        var paths = _runLogger.ResolvePaths(runId);

        await RunPlatformCodexAsync(
            command,
            sessionId,
            snapshot,
            request,
            paths,
            RunType: "smoke",
            TargetType: null,
            TargetId: null,
            StartedMessage: "Smoke run started.",
            FinishedMessage: "Smoke run finished.",
            Target: null,
            WorkspaceResult: null,
            stoppingToken);
    }

    private async Task RunAssignedWorkAsync(
        WorkerCommandResult command,
        string sessionId,
        WorkerEnvironmentSnapshot snapshot,
        CancellationToken stoppingToken)
    {
        var target = ResolveCommandTarget(command);
        if (target.TargetId is null || target.TargetType is null)
        {
            await FinishFailedRunWithoutCodexAsync(
                command,
                sessionId,
                WorkerPlatformStatuses.CodexFailed,
                target.RunType,
                target.TargetType,
                null,
                null,
                $"{target.RequiredJsonField} is required in worker command payload.",
                stoppingToken);
            return;
        }

        var runId = $"{target.RunType}-{target.TargetId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var prompt = await _apiClient.GetWorkPromptAsync(target.TargetType, target.TargetId, stoppingToken);
        var projectCode = ResolveProjectCode(target, prompt);
        var workspace = ResolveWorkWorkspace(snapshot.WorkspaceRoot, projectCode);
        var repositoryUrl = ResolveRepositoryUrl(target, prompt);
        var branch = ResolveBranch(target, prompt);
        var gitUsername = ResolveGitUsername(target, prompt, repositoryUrl);
        var gitAccessToken = ResolveGitAccessToken(target, prompt, repositoryUrl);
        var workspaceResult = await _gitWorkspaceManager.PrepareAsync(
            snapshot.WorkspaceRoot,
            projectCode,
            repositoryUrl,
            branch,
            gitUsername,
            gitAccessToken,
            stoppingToken);
        workspace = workspaceResult.WorkspacePath;

        if (!workspaceResult.Succeeded)
        {
            var failedRun = await FinishFailedRunWithoutCodexAsync(
                command,
                sessionId,
                WorkerPlatformStatuses.Blocked,
                target.RunType,
                target.TargetType,
                target.TargetId,
                workspace,
                workspaceResult.Error ?? "Workspace preparation failed.",
                stoppingToken);
            await ReportWorkspacePreparedAsync(sessionId, failedRun.Id, workspaceResult, CancellationToken.None);
            return;
        }

        if (!workspaceResult.RepositoryAvailable)
        {
            var blockedRun = await FinishFailedRunWithoutCodexAsync(
                command,
                sessionId,
                WorkerPlatformStatuses.Blocked,
                target.RunType,
                target.TargetType,
                target.TargetId,
                workspace,
                "Project repository is not configured; Worker blocked before starting Codex.",
                stoppingToken);
            await ReportWorkspacePreparedAsync(sessionId, blockedRun.Id, workspaceResult, CancellationToken.None);
            return;
        }

        var paths = _runLogger.ResolvePaths(runId);
        var executionPrompt = BuildCodexExecutionPrompt(
            prompt.Prompt,
            _options,
            snapshot,
            runId,
            workspace,
            paths,
            target,
            projectCode,
            workspaceResult);
        var request = new CodexRunRequest(
            runId,
            workspace,
            executionPrompt,
            _options.SandboxMode,
            SkipGitRepoCheck: false,
            TimeSpan.FromMinutes(_options.MaxRunMinutes),
            TimeSpan.FromSeconds(_options.CodexIdleTimeoutSeconds),
            _options.CodexExecutable);

        await RunPlatformCodexAsync(
            command,
            sessionId,
            snapshot,
            request,
            paths,
            target.RunType,
            target.TargetType,
            target.TargetId,
            $"{target.DisplayName} run started.",
            $"{target.DisplayName} run finished.",
            target,
            workspaceResult,
            stoppingToken);
    }

    private async Task RunPlatformCodexAsync(
        WorkerCommandResult command,
        string sessionId,
        WorkerEnvironmentSnapshot snapshot,
        CodexRunRequest request,
        RunPaths paths,
        string RunType,
        string? TargetType,
        string? TargetId,
        string StartedMessage,
        string FinishedMessage,
        WorkerCommandTarget? Target,
        WorkspacePreparationResult? WorkspaceResult,
        CancellationToken stoppingToken)
    {
        await _apiClient.StartCommandAsync(command.Id, new AckWorkerCommandRequest(sessionId), stoppingToken);
        if (!snapshot.CanEnterWorkLoop)
        {
            await FinishFailedRunWithoutCodexAsync(
                command,
                sessionId,
                WorkerPlatformStatuses.CodexFailed,
                RunType,
                TargetType,
                TargetId,
                request.WorkingDirectory,
                "Codex CLI is unavailable.",
                stoppingToken);
            return;
        }

        WorkerRunResult? platformRun = null;
        try
        {
            platformRun = await _apiClient.StartRunAsync(
                new StartWorkerRunRequest(
                    _options.WorkerId,
                    sessionId,
                    RunType,
                    WorkerPlatformStatuses.Running,
                    command.Id,
                    TargetType,
                    TargetId,
                    request.WorkingDirectory,
                    paths.PromptPath,
                    paths.StdoutPath,
                    paths.StderrPath,
                    paths.FinalPath,
                    paths.ManifestPath),
                stoppingToken);

            if (WorkspaceResult is not null)
            {
                await ReportWorkspacePreparedAsync(sessionId, platformRun.Id, WorkspaceResult, stoppingToken);
            }

            await ReportEventAsync(sessionId, platformRun.Id, "codex_started", "info", StartedMessage, stoppingToken);
            var result = await _codexProcessRunner.RunAsync(request, stoppingToken);
            if (result.Status == WorkerPlatformStatuses.Success && Target is not null && Target.TargetId is not null)
            {
                await _apiClient.CompleteWorkAsync(Target.TargetType!, Target.TargetId, CancellationToken.None);
            }

            await _apiClient.FinishRunAsync(
                platformRun.Id,
                new FinishWorkerRunRequest(
                    result.Status,
                    result.ExitCode,
                    result.TimedOut,
                    result.Error,
                    ResultJson: BuildRunResultJson(result)),
                CancellationToken.None);
            await ReportEventAsync(sessionId, platformRun.Id, "codex_finished", ResolveEventLevel(result.Status), FinishedMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (platformRun is not null)
            {
                await _apiClient.FinishRunAsync(
                    platformRun.Id,
                    new FinishWorkerRunRequest(
                        WorkerPlatformStatuses.CodexFailed,
                        ExitCode: null,
                        TimedOut: false,
                        Error: ex.Message,
                        ResultJson: null),
                    CancellationToken.None);
            }

            throw;
        }
    }

    private async Task<WorkerRunResult> FinishFailedRunWithoutCodexAsync(
        WorkerCommandResult command,
        string sessionId,
        string status,
        string runType,
        string? targetType,
        string? targetId,
        string? workspacePath,
        string error,
        CancellationToken stoppingToken)
    {
        var platformRun = await _apiClient.StartRunAsync(
            new StartWorkerRunRequest(
                _options.WorkerId,
                sessionId,
                RunType: runType,
                Status: WorkerPlatformStatuses.Running,
                CommandId: command.Id,
                TargetType: targetType,
                TargetId: targetId,
                WorkspacePath: workspacePath,
                PromptPath: null,
                StdoutPath: null,
                StderrPath: null,
                FinalPath: null,
                ManifestPath: null),
            stoppingToken);
        return await _apiClient.FinishRunAsync(
            platformRun.Id,
            new FinishWorkerRunRequest(status, null, false, error, null),
            CancellationToken.None);
    }

    private async Task RunSmokeAsync(WorkerEnvironmentSnapshot snapshot, CancellationToken stoppingToken)
    {
        var runId = "smoke-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var workspace = ResolveSmokeWorkspace(snapshot.WorkspaceRoot);
        var request = new CodexRunRequest(
            runId,
            workspace,
            _options.SmokePrompt,
            _options.SandboxMode,
            SkipGitRepoCheck: true,
            TimeSpan.FromMinutes(_options.MaxRunMinutes),
            TimeSpan.FromSeconds(_options.CodexIdleTimeoutSeconds),
            _options.CodexExecutable);

        var result = await _codexProcessRunner.RunAsync(request, stoppingToken);
        try
        {
            AppRealization.TraceLog.Write(
                AppRealization.JSON.Serialize(new
                {
                    level = "Information",
                    message = "Smoke run finished.",
                    runId = result.RunId,
                    status = result.Status,
                    exitCode = result.ExitCode,
                    finalPath = result.FinalPath
                }),
                new Dictionary<string, string>()
                {
                    { "runId", result.RunId },
                    { "status", result.Status },
                    { "exitCode", result.ExitCode?.ToString() ?? "<null>" },
                    { "finalPath", result.FinalPath }
                });
        }
        catch
        {
        }
    }

    private string ResolveSmokeWorkspace(string workspaceRoot)
    {
        var projectCode = string.IsNullOrWhiteSpace(_options.ProjectCode) ? "_smoke" : _options.ProjectCode;
        return ResolveWorkWorkspace(workspaceRoot, projectCode);
    }

    private string ResolveWorkWorkspace(string workspaceRoot, string? projectCode)
    {
        projectCode = string.IsNullOrWhiteSpace(projectCode) ? "_unscoped" : projectCode.Trim();
        var path = Path.Combine(workspaceRoot, projectCode);
        Directory.CreateDirectory(path);
        return path;
    }

    internal static WorkerCommandTarget ResolveCommandTarget(WorkerCommandResult command)
    {
        var payload = ParsePayload(command.PayloadJson);
        if (command.CommandType == WorkerPlatformCommandTypes.StartTask)
        {
            return new WorkerCommandTarget(
                "task",
                "task",
                ReadPayloadString(payload, "taskId", "task_id"),
                "taskId",
                ReadPayloadString(payload, "projectCode", "project_code"),
                ReadPayloadString(payload, "repositoryUrl", "repository_url", "repositoryReference", "repository_reference"),
                ReadPayloadString(payload, "branch"),
                "Task");
        }

        if (command.CommandType == WorkerPlatformCommandTypes.StartBug)
        {
            return new WorkerCommandTarget(
                "bug",
                "bug",
                ReadPayloadString(payload, "bugId", "bug_id"),
                "bugId",
                ReadPayloadString(payload, "projectCode", "project_code"),
                ReadPayloadString(payload, "repositoryUrl", "repository_url", "repositoryReference", "repository_reference"),
                ReadPayloadString(payload, "branch"),
                "Bug");
        }

        return new WorkerCommandTarget("command", null, null, "targetId", null, null, null, "Command");
    }

    private static JsonObject? ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        return JsonNode.Parse(payloadJson, nodeOptions: new JsonNodeOptions { PropertyNameCaseInsensitive = true }) as JsonObject;
    }

    private static string? ReadPayloadString(JsonObject? payload, params string[] names)
    {
        if (payload is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (payload.TryGetPropertyValue(name, out var value))
            {
                return value?.GetValue<string>();
            }
        }

        return null;
    }

    private static string? ResolveProjectCode(WorkerCommandTarget target, WorkerPromptResult prompt)
    {
        return target.ProjectCode ?? prompt.Context?.ProjectCode;
    }

    private static string? ResolveRepositoryUrl(WorkerCommandTarget target, WorkerPromptResult prompt)
    {
        return target.RepositoryUrl ?? prompt.Context?.RepositoryUrl;
    }

    private static string? ResolveBranch(WorkerCommandTarget target, WorkerPromptResult prompt)
    {
        return target.Branch ?? prompt.Context?.RepositoryDefaultBranch;
    }

    private static string? ResolveGitUsername(
        WorkerCommandTarget target,
        WorkerPromptResult prompt,
        string? repositoryUrl)
    {
        return CanUsePromptGitConfig(target, prompt, repositoryUrl)
            ? prompt.Context?.GitUsername
            : null;
    }

    private static string? ResolveGitAccessToken(
        WorkerCommandTarget target,
        WorkerPromptResult prompt,
        string? repositoryUrl)
    {
        return CanUsePromptGitConfig(target, prompt, repositoryUrl)
            ? prompt.Context?.GitAccessToken
            : null;
    }

    private static bool CanUsePromptGitConfig(
        WorkerCommandTarget target,
        WorkerPromptResult prompt,
        string? repositoryUrl)
    {
        if (prompt.Context is null || string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(target.RepositoryUrl) ||
            string.Equals(target.RepositoryUrl, prompt.Context.RepositoryUrl, StringComparison.Ordinal);
    }

    private static string BuildRunResultJson(CodexRunResult result)
    {
        return JsonSerializer.Serialize(
            new
            {
                result.RunId,
                result.RunDirectory,
                result.StdoutPath,
                result.StderrPath,
                result.FinalPath,
                result.StartedAt,
                result.CompletedAt
            },
            JsonOptions);
    }

    internal static string BuildCodexExecutionPrompt(
        string platformPrompt,
        WorkerOptions options,
        WorkerEnvironmentSnapshot snapshot,
        string runId,
        string workingDirectory,
        RunPaths paths,
        WorkerCommandTarget? target,
        string? projectCode,
        WorkspacePreparationResult? workspaceResult)
    {
        var builder = new StringBuilder();
        builder.AppendLine("AgentSprint.Worker execution context:");
        builder.AppendLine($"- Worker ID: {options.WorkerId}");
        builder.AppendLine($"- Worker name: {options.WorkerName}");
        builder.AppendLine($"- Project ID: {options.ProjectId ?? string.Empty}");
        builder.AppendLine($"- Project code: {projectCode ?? options.ProjectCode ?? target?.ProjectCode ?? string.Empty}");
        builder.AppendLine($"- Target type: {target?.TargetType ?? string.Empty}");
        builder.AppendLine($"- Target ID: {target?.TargetId ?? string.Empty}");
        builder.AppendLine($"- Run ID: {runId}");
        builder.AppendLine($"- Workspace root: {snapshot.WorkspaceRoot}");
        builder.AppendLine($"- Current workspace path: {workingDirectory}");
        builder.AppendLine($"- Runs/log root: {snapshot.RunsRoot}");
        builder.AppendLine($"- Current run directory: {paths.RunDirectory}");
        builder.AppendLine($"- Prompt path: {paths.PromptPath}");
        builder.AppendLine($"- Stdout log path: {paths.StdoutPath}");
        builder.AppendLine($"- Stderr log path: {paths.StderrPath}");
        builder.AppendLine($"- Final response path: {paths.FinalPath}");
        builder.AppendLine($"- Run manifest path: {paths.ManifestPath}");
        builder.AppendLine($"- Codex Home: {snapshot.CodexHome}");
        builder.AppendLine($"- Codex config exists: {(snapshot.ConfigTomlExists ? "true" : "false")}");
        builder.AppendLine($"- Sandbox mode: {options.SandboxMode}");
        builder.AppendLine($"- Run smoke on startup: {(options.RunSmokeOnStartup ? "true" : "false")}");
        builder.AppendLine($"- Smoke prompt: {options.SmokePrompt}");
        builder.AppendLine($"- Codex executable: {options.CodexExecutable}");
        builder.AppendLine($"- Codex provider: {options.CodexProvider}");
        builder.AppendLine($"- Codex model: {options.CodexModel}");
        builder.AppendLine($"- OpenAI base URL: {options.OpenAiBaseUrl ?? string.Empty}");
        builder.AppendLine($"- Max run minutes: {options.MaxRunMinutes}");
        builder.AppendLine($"- Codex idle timeout seconds: {options.CodexIdleTimeoutSeconds}");
        builder.AppendLine($"- Poll interval seconds: {options.PollIntervalSeconds}");
        builder.AppendLine($"- Idle max interval seconds: {options.IdleMaxIntervalSeconds}");
        builder.AppendLine($"- Config version: {options.ConfigVersion}");

        if (workspaceResult is not null)
        {
            builder.AppendLine($"- Repository available: {(workspaceResult.RepositoryAvailable ? "true" : "false")}");
            builder.AppendLine($"- Git branch: {workspaceResult.Branch ?? string.Empty}");
            builder.AppendLine($"- Git commit: {workspaceResult.Commit ?? string.Empty}");
            builder.AppendLine($"- Workspace dirty: {(workspaceResult.Dirty ? "true" : "false")}");
        }

        builder.AppendLine();
        builder.AppendLine("Use the current workspace path as the project root. The log paths above are local Worker artifacts for this run; do not write secrets to them or to the final response.");
        builder.AppendLine();
        builder.AppendLine(platformPrompt.Trim());
        return builder.ToString().Trim();
    }

    private Task ReportEventAsync(
        string sessionId,
        string? runId,
        string eventType,
        string level,
        string message,
        CancellationToken cancellationToken)
    {
        return ReportEventAsync(sessionId, runId, eventType, level, message, payloadJson: null, cancellationToken);
    }

    private Task ReportEventAsync(
        string sessionId,
        string? runId,
        string eventType,
        string level,
        string message,
        string? payloadJson,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _akkaClusterService.Tell(
                WorkerActorNames.EventReporterRegisteredName,
                new WorkerEventReportMessage(
                    _options.WorkerId,
                    eventType,
                    message,
                    sessionId,
                    runId,
                    level,
                    payloadJson));
        }
        catch (Exception ex)
        {
            try
            {
                AppRealization.TraceLog.Write(
                    AppRealization.JSON.Serialize(new
                    {
                        level = "Warning",
                        message = "Failed to enqueue worker event.",
                        eventType,
                        workerId = _options.WorkerId,
                        sessionId,
                        runId,
                        exception = ex.ToString()
                    }),
                    new Dictionary<string, string>()
                    {
                        { "eventType", eventType },
                        { "workerId", _options.WorkerId },
                        { "sessionId", sessionId },
                        { "runId", runId ?? "<null>" }
                    });
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }

    private async Task ReportAkkaClusterStartedAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentNode = _akkaClusterService.GetCurrentNode();
            var payloadJson = JsonSerializer.Serialize(
                new
                {
                    currentNode.Address,
                    currentNode.Status,
                    currentNode.Roles,
                    currentNode.IsAvailable,
                    Actor = WorkerActorNames.EventReporterRegisteredName
                },
                JsonOptions);

            await ReportEventAsync(
                sessionId,
                null,
                WorkerEventTypes.AkkaClusterStarted,
                currentNode.IsAvailable ? "info" : "warn",
                "Akka cluster node started.",
                payloadJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            AppRealization.TraceLog.Write(
                AppRealization.JSON.Serialize(new
                {
                    level = "Warning",
                    message = "Failed to collect Akka cluster node status.",
                    workerId = _options.WorkerId,
                    sessionId,
                    exception = ex.ToString()
                }),
                new Dictionary<string, string>()
                {
                    { "eventType", WorkerEventTypes.AkkaClusterStarted },
                    { "workerId", _options.WorkerId },
                    { "sessionId", sessionId }
                });
        }
    }

    private async Task ReportWorkspacePreparedAsync(
        string sessionId,
        string? runId,
        WorkspacePreparationResult result,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                result.WorkspacePath,
                result.RepositoryAvailable,
                result.Branch,
                result.Commit,
                result.Dirty,
                result.Error
            },
            JsonOptions);
        await ReportEventAsync(
            sessionId,
            runId,
            "workspace_prepared",
            result.Succeeded ? "info" : "error",
            result.Succeeded
                ? "Worker workspace prepared."
                : "Worker workspace preparation failed.",
            payload,
            cancellationToken);
    }

    private static string ResolveHeartbeatStatus(WorkerEnvironmentSnapshot snapshot)
    {
        if (!snapshot.CanEnterWorkLoop)
        {
            return WorkerPlatformStatuses.Error;
        }

        return snapshot.IsCodexAuthenticated
            ? WorkerPlatformStatuses.Idle
            : WorkerPlatformStatuses.AuthRequired;
    }

    private static string? ResolveProbeOutput(CommandProbeResult result)
    {
        if (!result.Succeeded)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(result.Stdout)
            ? null
            : result.Stdout.Trim();
    }

    private static string ResolveEventLevel(string status)
    {
        return status == WorkerPlatformStatuses.Success ? "info" : "error";
    }

    internal sealed record WorkerCommandTarget(
        string RunType,
        string? TargetType,
        string? TargetId,
        string RequiredJsonField,
        string? ProjectCode,
        string? RepositoryUrl,
        string? Branch,
        string DisplayName);
}
