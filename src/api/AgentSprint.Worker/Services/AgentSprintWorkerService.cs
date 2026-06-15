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
    private bool _stopAfterCurrent;

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
            WorkerDiagnostics.Info("Worker拉取运行配置", $"apiBaseUrl={_agentSprintOptions.ApiBaseUrl}");
            var config = await _apiClient.GetRuntimeConfigAsync(stoppingToken);
            await _runtimeConfigApplier.ApplyAsync(config, stoppingToken);
            _apiClient.UseAgentToken(config.AgentToken);
        }

        var snapshot = await _environmentProbe.ProbeAsync(stoppingToken);
        var session = await RegisterSessionAsync(snapshot, stoppingToken);
        WorkerDiagnostics.Info(
            "Worker会话注册完成",
            $"sessionId={session.Id}, workerId={session.WorkerId}, status={session.Status}, canEnterWorkLoop={snapshot.CanEnterWorkLoop}, isCodexAuthenticated={snapshot.IsCodexAuthenticated}");
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
            WorkerDiagnostics.Info(
                "Worker心跳完成",
                $"sessionId={session.Id}, heartbeatStatus={heartbeatStatus}, nextIntervalSeconds={heartbeat.NextIntervalSeconds}, commandCount={heartbeat.Commands.Count}, commands={string.Join(",", heartbeat.Commands.Select(item => item.CommandType + ":" + item.Id + ":" + item.Status))}");

            foreach (var command in heartbeat.Commands)
            {
                var shouldContinue = await HandleCommandAsync(command, session.Id, snapshot, stoppingToken);
                if (!shouldContinue)
                {
                    return;
                }
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

    private async Task<bool> HandleCommandAsync(
        WorkerCommandResult command,
        string sessionId,
        WorkerEnvironmentSnapshot snapshot,
        CancellationToken stoppingToken)
    {
        WorkerDiagnostics.Info(
            "Worker收到命令",
            $"commandId={command.Id}, commandType={command.CommandType}, commandStatus={command.Status}, sessionId={sessionId}, payload={WorkerDiagnostics.TrimAndRedact(command.PayloadJson, 2000)}");
        await _apiClient.AckCommandAsync(command.Id, new AckWorkerCommandRequest(sessionId), stoppingToken);
        WorkerDiagnostics.Info("Worker命令ACK完成", $"commandId={command.Id}, commandType={command.CommandType}, sessionId={sessionId}");

        if (command.CommandType is WorkerPlatformCommandTypes.ReloadConfig or
            WorkerPlatformCommandTypes.StopAfterCurrent or
            WorkerPlatformCommandTypes.CancelCurrentRun)
        {
            return await HandleControlCommandAsync(command, sessionId, stoppingToken);
        }

        if (!snapshot.CanEnterWorkLoop || !snapshot.IsCodexAuthenticated)
        {
            WorkerDiagnostics.Warn(
                "Worker命令执行前置检查失败",
                $"commandId={command.Id}, canEnterWorkLoop={snapshot.CanEnterWorkLoop}, isCodexAuthenticated={snapshot.IsCodexAuthenticated}");
            await FinishFailedRunWithoutCodexAsync(
                command,
                sessionId,
                WorkerPlatformStatuses.Blocked,
                "command",
                null,
                null,
                null,
                !snapshot.CanEnterWorkLoop
                    ? "Codex CLI is unavailable."
                    : "Codex authentication is required before executing worker commands.",
                stoppingToken);
            return true;
        }

        if (command.CommandType == WorkerPlatformCommandTypes.Smoke)
        {
            await RunPlatformSmokeAsync(command, sessionId, snapshot, stoppingToken);
            return !_stopAfterCurrent;
        }

        if (command.CommandType is WorkerPlatformCommandTypes.StartTask or WorkerPlatformCommandTypes.StartBug)
        {
            await RunAssignedWorkAsync(command, sessionId, snapshot, stoppingToken);
            return !_stopAfterCurrent;
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
        return true;
    }

    private async Task<bool> HandleControlCommandAsync(
        WorkerCommandResult command,
        string sessionId,
        CancellationToken stoppingToken)
    {
        var run = await _apiClient.StartRunAsync(
            new StartWorkerRunRequest(
                _options.WorkerId,
                sessionId,
                RunType: "command",
                Status: WorkerPlatformStatuses.Running,
                CommandId: command.Id,
                TargetType: null,
                TargetId: null,
                WorkspacePath: null,
                PromptPath: null,
                StdoutPath: null,
                StderrPath: null,
                FinalPath: null,
                ManifestPath: null),
            stoppingToken);

        string message;
        var shouldContinue = true;
        if (command.CommandType == WorkerPlatformCommandTypes.ReloadConfig)
        {
            var config = await _apiClient.GetRuntimeConfigAsync(stoppingToken);
            await _runtimeConfigApplier.ApplyAsync(config, stoppingToken);
            _apiClient.UseAgentToken(config.AgentToken);
            message = "Worker runtime config reloaded.";
        }
        else if (command.CommandType == WorkerPlatformCommandTypes.StopAfterCurrent)
        {
            _stopAfterCurrent = true;
            message = "Worker will stop after the current command.";
            shouldContinue = false;
        }
        else
        {
            message = "No running Codex process is active in this polling loop.";
        }

        await _apiClient.FinishRunAsync(
            run.Id,
            new FinishWorkerRunRequest(
                WorkerPlatformStatuses.Success,
                ExitCode: 0,
                TimedOut: false,
                Error: null,
                ResultJson: JsonSerializer.Serialize(new { message }, JsonOptions)),
            CancellationToken.None);
        return shouldContinue;
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
            GitUsername: null,
            GitAccessToken: null,
            stoppingToken);
    }

    private async Task RunAssignedWorkAsync(
        WorkerCommandResult command,
        string sessionId,
        WorkerEnvironmentSnapshot snapshot,
        CancellationToken stoppingToken)
    {
        var target = ResolveCommandTarget(command);
        WorkerDiagnostics.Info(
            "Worker解析任务目标",
            $"commandId={command.Id}, runType={target.RunType}, targetType={target.TargetType ?? string.Empty}, targetId={target.TargetId ?? string.Empty}, projectCode={target.ProjectCode ?? string.Empty}, repositoryUrl={target.RepositoryUrl ?? string.Empty}, branch={target.Branch ?? string.Empty}");
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
        WorkerDiagnostics.Info("Worker获取任务提示词开始", $"commandId={command.Id}, runId={runId}, targetType={target.TargetType}, targetId={target.TargetId}");
        WorkerPromptResult prompt;
        try
        {
            prompt = await _apiClient.GetWorkPromptAsync(target.TargetType, target.TargetId, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WorkerDiagnostics.Warn(
                "Worker获取任务提示词失败",
                $"commandId={command.Id}, runId={runId}, targetType={target.TargetType}, targetId={target.TargetId}, error={ex.Message}");
            await FinishFailedRunWithoutCodexAsync(
                command,
                sessionId,
                WorkerPlatformStatuses.Blocked,
                target.RunType,
                target.TargetType,
                target.TargetId,
                null,
                ex.Message,
                stoppingToken);
            return;
        }
        WorkerDiagnostics.Info(
            "Worker获取任务提示词完成",
            $"commandId={command.Id}, runId={runId}, targetType={prompt.TargetType}, targetId={prompt.TargetId}, templateCode={prompt.TemplateCode}, promptLength={prompt.Prompt.Length}, contextProjectCode={prompt.Context?.ProjectCode ?? string.Empty}, repositoryUrl={prompt.Context?.RepositoryUrl ?? string.Empty}, defaultBranch={prompt.Context?.RepositoryDefaultBranch ?? string.Empty}, hasGitAccessToken={!string.IsNullOrWhiteSpace(prompt.Context?.GitAccessToken)}");
        var projectCode = ResolveProjectCode(target, prompt);
        var workspace = ResolveWorkWorkspace(snapshot.WorkspaceRoot, projectCode);
        var repositoryUrl = ResolveRepositoryUrl(target, prompt);
        var branch = ResolveBranch(target, prompt);
        var gitUsername = ResolveGitUsername(target, prompt, repositoryUrl);
        var gitAccessToken = ResolveGitAccessToken(target, prompt, repositoryUrl);
        WorkerDiagnostics.Info(
            "Worker准备Git工作区开始",
            $"runId={runId}, workspaceRoot={snapshot.WorkspaceRoot}, projectCode={projectCode ?? string.Empty}, repositoryUrl={repositoryUrl ?? string.Empty}, branch={branch ?? string.Empty}, hasGitUsername={!string.IsNullOrWhiteSpace(gitUsername)}, hasGitAccessToken={!string.IsNullOrWhiteSpace(gitAccessToken)}");
        var workspaceResult = await _gitWorkspaceManager.PrepareAsync(
            snapshot.WorkspaceRoot,
            projectCode,
            repositoryUrl,
            branch,
            gitUsername,
            gitAccessToken,
            stoppingToken);
        workspace = workspaceResult.WorkspacePath;
        WorkerDiagnostics.Info(
            "Worker准备Git工作区完成",
            $"runId={runId}, succeeded={workspaceResult.Succeeded}, repositoryAvailable={workspaceResult.RepositoryAvailable}, workspacePath={workspaceResult.WorkspacePath}, branch={workspaceResult.Branch ?? string.Empty}, commit={workspaceResult.Commit ?? string.Empty}, dirty={workspaceResult.Dirty}, error={workspaceResult.Error ?? string.Empty}");

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

        if (workspaceResult.Dirty)
        {
            var blockedRun = await FinishFailedRunWithoutCodexAsync(
                command,
                sessionId,
                WorkerPlatformStatuses.Blocked,
                target.RunType,
                target.TargetType,
                target.TargetId,
                workspace,
                "Project workspace has uncommitted changes before task execution; Worker blocked to avoid mixing stale local changes into this run.",
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
        WorkerDiagnostics.Info(
            "Worker构建Codex提示词完成",
            $"runId={runId}, promptLength={executionPrompt.Length}, promptPath={paths.PromptPath}, workspace={workspace}");
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
            gitUsername,
            gitAccessToken,
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
        string? GitUsername,
        string? GitAccessToken,
        CancellationToken stoppingToken)
    {
        WorkerDiagnostics.Info(
            "Worker启动平台Codex运行",
            $"commandId={command.Id}, runType={RunType}, targetType={TargetType ?? string.Empty}, targetId={TargetId ?? string.Empty}, localRunId={request.RunId}, workingDirectory={request.WorkingDirectory}, promptPath={paths.PromptPath}, stdoutPath={paths.StdoutPath}, stderrPath={paths.StderrPath}, finalPath={paths.FinalPath}");
        await _apiClient.StartCommandAsync(command.Id, new AckWorkerCommandRequest(sessionId), stoppingToken);
        WorkerDiagnostics.Info("Worker命令START完成", $"commandId={command.Id}, commandType={command.CommandType}, sessionId={sessionId}");
        if (!snapshot.CanEnterWorkLoop)
        {
            WorkerDiagnostics.Warn(
                "Worker阻止Codex运行",
                $"commandId={command.Id}, localRunId={request.RunId}, reason=Codex CLI is unavailable.");
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
        CancellationTokenSource? runCancellation = null;
        Task? heartbeatTask = null;
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
            WorkerDiagnostics.Info(
                "平台Run已创建",
                $"platformRunId={platformRun.Id}, localRunId={request.RunId}, status={platformRun.Status}, workerId={platformRun.WorkerId}, sessionId={platformRun.SessionId}");

            if (WorkspaceResult is not null)
            {
                await ReportWorkspacePreparedAsync(sessionId, platformRun.Id, WorkspaceResult, stoppingToken);
            }

            await ReportEventAsync(sessionId, platformRun.Id, "codex_started", "info", StartedMessage, stoppingToken);
            WorkerDiagnostics.Info("Codex开始事件已上报", $"platformRunId={platformRun.Id}, localRunId={request.RunId}, message={StartedMessage}");
            runCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            heartbeatTask = MaintainBusyHeartbeatAsync(sessionId, platformRun.Id, runCancellation, stoppingToken);
            WorkerDiagnostics.Info("Codex本地执行开始", $"platformRunId={platformRun.Id}, localRunId={request.RunId}");
            var result = await _codexProcessRunner.RunAsync(request, runCancellation.Token);
            WorkspacePublishResult? publishResult = null;
            WorkerDiagnostics.Info(
                "Codex本地执行结束",
                $"platformRunId={platformRun.Id}, localRunId={result.RunId}, status={result.Status}, exitCode={result.ExitCode?.ToString() ?? "<null>"}, timedOut={result.TimedOut}, error={result.Error ?? string.Empty}, runDirectory={result.RunDirectory}");
            if (result.Status == WorkerPlatformStatuses.Success &&
                WorkspaceResult?.RepositoryAvailable == true &&
                !string.IsNullOrWhiteSpace(WorkspaceResult.RepositoryUrl))
            {
                WorkerDiagnostics.Info(
                    "Worker发布Git改动开始",
                    $"platformRunId={platformRun.Id}, localRunId={result.RunId}, workspace={request.WorkingDirectory}, branch={WorkspaceResult.Branch ?? string.Empty}");
                publishResult = await _gitWorkspaceManager.PublishAsync(
                    request.WorkingDirectory,
                    WorkspaceResult.RepositoryUrl,
                    GitUsername,
                    GitAccessToken,
                    BuildWorkerCommitMessage(Target, result),
                    (conflict, token) => ResolveGitConflictWithCodexAsync(conflict, request, paths, token),
                    CancellationToken.None);
                WorkerDiagnostics.Info(
                    "Worker发布Git改动结束",
                    $"platformRunId={platformRun.Id}, succeeded={publishResult.Succeeded}, hasChanges={publishResult.HasChanges}, pushed={publishResult.Pushed}, conflictResolved={publishResult.ConflictResolved}, branch={publishResult.Branch ?? string.Empty}, commit={publishResult.Commit ?? string.Empty}, error={publishResult.Error ?? string.Empty}");
                await ReportWorkspacePublishedAsync(sessionId, platformRun.Id, publishResult, CancellationToken.None);
                if (!publishResult.Succeeded)
                {
                    result = result with
                    {
                        Status = WorkerPlatformStatuses.Blocked,
                        Error = publishResult.Error ?? "Git publish failed after Codex completed."
                    };
                }
            }

            if (result.Status == WorkerPlatformStatuses.Success && Target is not null && Target.TargetId is not null)
            {
                WorkerDiagnostics.Info(
                    "Worker完成业务目标开始",
                    $"platformRunId={platformRun.Id}, targetType={Target.TargetType}, targetId={Target.TargetId}");
                await _apiClient.CompleteWorkAsync(Target.TargetType!, Target.TargetId, CancellationToken.None);
                WorkerDiagnostics.Info(
                    "Worker完成业务目标结束",
                    $"platformRunId={platformRun.Id}, targetType={Target.TargetType}, targetId={Target.TargetId}");
            }

            await _apiClient.FinishRunAsync(
                platformRun.Id,
                new FinishWorkerRunRequest(
                    result.Status,
                    result.ExitCode,
                    result.TimedOut,
                    result.Error,
                    ResultJson: BuildRunResultJson(result, publishResult)),
                CancellationToken.None);
            WorkerDiagnostics.Info(
                "平台Run结束已上报",
                $"platformRunId={platformRun.Id}, localRunId={result.RunId}, status={result.Status}, exitCode={result.ExitCode?.ToString() ?? "<null>"}, error={result.Error ?? string.Empty}");
            await ReportEventAsync(sessionId, platformRun.Id, "codex_finished", ResolveEventLevel(result.Status), FinishedMessage, CancellationToken.None);
            WorkerDiagnostics.Info("Codex结束事件已上报", $"platformRunId={platformRun.Id}, localRunId={result.RunId}, message={FinishedMessage}");
        }
        catch (Exception ex)
        {
            WorkerDiagnostics.Error(
                "Worker平台Codex运行异常",
                $"commandId={command.Id}, platformRunId={platformRun?.Id ?? string.Empty}, localRunId={request.RunId}, error={ex}");
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

            await ReportEventAsync(
                sessionId,
                platformRun?.Id,
                "worker_command_failed",
                "error",
                ex.Message,
                CancellationToken.None);
        }
        finally
        {
            if (runCancellation is not null && heartbeatTask is not null)
            {
                await StopBusyHeartbeatAsync(runCancellation, heartbeatTask);
                runCancellation.Dispose();
            }
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
        WorkerDiagnostics.Warn(
            "Worker不启动Codex直接结束Run",
            $"commandId={command.Id}, status={status}, runType={runType}, targetType={targetType ?? string.Empty}, targetId={targetId ?? string.Empty}, workspacePath={workspacePath ?? string.Empty}, error={error}");
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

    private async Task MaintainBusyHeartbeatAsync(
        string sessionId,
        string runId,
        CancellationTokenSource runCancellation,
        CancellationToken stoppingToken)
    {
        while (!runCancellation.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            WorkerHeartbeatResult heartbeat;
            try
            {
                heartbeat = await _apiClient.HeartbeatAsync(
                    new WorkerHeartbeatRequest(
                        _options.WorkerId,
                        sessionId,
                        WorkerPlatformStatuses.Busy,
                        CurrentRunId: runId,
                        ErrorSummary: null),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            foreach (var command in heartbeat.Commands)
            {
                WorkerDiagnostics.Info(
                    "Worker忙碌心跳收到控制命令",
                    $"runId={runId}, commandId={command.Id}, commandType={command.CommandType}, commandStatus={command.Status}, payload={WorkerDiagnostics.TrimAndRedact(command.PayloadJson, 2000)}");
                if (command.CommandType == WorkerPlatformCommandTypes.CancelCurrentRun)
                {
                    runCancellation.Cancel();
                    await HandleControlCommandAsync(command, sessionId, CancellationToken.None);
                    return;
                }

                if (command.CommandType == WorkerPlatformCommandTypes.StopAfterCurrent)
                {
                    _stopAfterCurrent = true;
                    await HandleControlCommandAsync(command, sessionId, stoppingToken);
                }
                else if (command.CommandType == WorkerPlatformCommandTypes.ReloadConfig)
                {
                    await HandleControlCommandAsync(command, sessionId, stoppingToken);
                }
            }

            var delaySeconds = heartbeat.NextIntervalSeconds > 0
                ? heartbeat.NextIntervalSeconds
                : _options.PollIntervalSeconds;
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), runCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static async Task StopBusyHeartbeatAsync(CancellationTokenSource runCancellation, Task heartbeatTask)
    {
        if (!runCancellation.IsCancellationRequested)
        {
            runCancellation.Cancel();
        }

        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunSmokeAsync(WorkerEnvironmentSnapshot snapshot, CancellationToken stoppingToken)
    {
        var runId = "smoke-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var workspace = ResolveSmokeWorkspace(snapshot.WorkspaceRoot);
        WorkerDiagnostics.Info(
            "Worker启动本地Smoke",
            $"runId={runId}, workspace={workspace}, promptLength={_options.SmokePrompt.Length}, sandboxMode={_options.SandboxMode}, timeoutMinutes={_options.MaxRunMinutes}");
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
        WorkerDiagnostics.Info(
            "Worker本地Smoke结束",
            $"runId={result.RunId}, status={result.Status}, exitCode={result.ExitCode?.ToString() ?? "<null>"}, timedOut={result.TimedOut}, error={result.Error ?? string.Empty}, finalPath={result.FinalPath}");
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

    private async Task<GitConflictResolutionResult> ResolveGitConflictWithCodexAsync(
        GitConflictResolutionRequest conflict,
        CodexRunRequest originalRequest,
        RunPaths originalPaths,
        CancellationToken cancellationToken)
    {
        var conflictRunId = originalRequest.RunId + "-conflict-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var prompt = BuildConflictResolutionPrompt(conflict, originalPaths);
        var request = new CodexRunRequest(
            conflictRunId,
            conflict.WorkspacePath,
            prompt,
            originalRequest.SandboxMode,
            SkipGitRepoCheck: false,
            originalRequest.Timeout,
            originalRequest.IdleTimeout,
            originalRequest.CodexExecutable);
        var result = await _codexProcessRunner.RunAsync(request, cancellationToken);
        if (result.Status != WorkerPlatformStatuses.Success)
        {
            return new GitConflictResolutionResult(false, result.Error ?? "Codex conflict resolution failed.");
        }

        return new GitConflictResolutionResult(true, null);
    }

    private static string BuildConflictResolutionPrompt(
        GitConflictResolutionRequest conflict,
        RunPaths originalPaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine("AgentSprint.Worker Git conflict resolution:");
        builder.AppendLine($"- Workspace path: {conflict.WorkspacePath}");
        builder.AppendLine($"- Branch: {conflict.Branch}");
        builder.AppendLine($"- Operation: {conflict.Operation}");
        builder.AppendLine($"- Original run directory: {originalPaths.RunDirectory}");
        builder.AppendLine("- Conflict files:");
        foreach (var file in conflict.ConflictFiles)
        {
            builder.AppendLine($"  - {file}");
        }

        builder.AppendLine();
        builder.AppendLine("Resolve the Git merge conflicts in the listed files only.");
        builder.AppendLine("Do not run git commit or git push. AgentSprint.Worker will stage, commit, and push after you finish.");
        builder.AppendLine("Remove all Git conflict markers: <<<<<<<, =======, and >>>>>>>.");
        builder.AppendLine("Do not completely delete either side of a conflict. If one side should not be active code, keep it as a clear code comment near the resolved code.");
        builder.AppendLine("Preserve behavior from both local task changes and remote branch changes whenever possible, then run focused verification if practical.");
        builder.AppendLine();
        builder.AppendLine("Merge error:");
        builder.AppendLine(conflict.Error);
        return builder.ToString().Trim();
    }

    private static string BuildWorkerCommitMessage(
        WorkerCommandTarget? target,
        CodexRunResult result)
    {
        var targetText = target is null || string.IsNullOrWhiteSpace(target.TargetId)
            ? result.RunId
            : $"{target.TargetType ?? target.RunType} {target.TargetId}";
        return $"AgentSprint worker update: {targetText}";
    }

    private static string BuildRunResultJson(CodexRunResult result, WorkspacePublishResult? publishResult = null)
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
                result.CompletedAt,
                GitPublish = publishResult
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

    private async Task ReportWorkspacePublishedAsync(
        string sessionId,
        string? runId,
        WorkspacePublishResult result,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                result.WorkspacePath,
                result.HasChanges,
                result.Pushed,
                result.ConflictResolved,
                result.Branch,
                result.Commit,
                result.Error
            },
            JsonOptions);
        await ReportEventAsync(
            sessionId,
            runId,
            "workspace_published",
            result.Succeeded ? "info" : "error",
            result.Succeeded
                ? "Worker workspace published."
                : "Worker workspace publish failed.",
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
