using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;

using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Impls.AgileServices;
using AgentSprint.Service.Services.AgileServices;
using AgentSprint.Service.Services.SecurityServices;

namespace AgentSprint.Tests;

public sealed class DigitalWorkerServiceTests
{
    [Fact]
    public async Task Management_CreateWorker_GeneratesCodeAndNormalizesPolicy()
    {
        var domains = new DigitalWorkerTestDomains();
        var service = domains.CreateManagementService();

        var worker = await service.CreateWorkerAsync(
            new CreateDigitalWorkerRequest(
                "Codex Worker",
                "agent-1",
                EmployeeType: DigitalWorkerEmployeeTypes.Operations,
                ProjectIds: ["project-1", "project-1", "project-2"],
                SkillIds: ["skill-1"],
                MaxConcurrentRuns: 99,
                HeartbeatTimeoutSeconds: 45),
            "admin");

        Assert.StartsWith("dw-", worker.Code);
        Assert.Equal(DigitalWorkerEmployeeTypes.Operations, worker.EmployeeType);
        Assert.Equal(["project-1", "project-2"], worker.ProjectIds);
        Assert.Equal(["skill-1"], worker.SkillIds);
        Assert.Equal(10, worker.MaxConcurrentRuns);
        Assert.Equal(90, worker.HeartbeatTimeoutSeconds);
        Assert.Equal("/workspaces", worker.WorkspaceRoot);
        Assert.Equal("gpt-5.4", worker.CodexModel);
    }

    [Fact]
    public async Task Runtime_GetRuntimeConfig_ReturnsPlatformManagedSettingsAndToken()
    {
        var domains = new DigitalWorkerTestDomains();
        domains.ConfigurationService.SetAiPlatform(new AiPlatformRuntimeResult(
            "openai",
            "OpenAI",
            "openai",
            "gpt-platform",
            "platform-api-key",
            "https://platform.example/v1",
            1));
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var token = new AgentTokenEntity
        {
            Name = "worker-token",
            TokenValue = "agent-token-value",
            OwnerUserId = "agent-1",
            CreatedBy = "admin",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Status = 1
        };
        await domains.AgentTokens.CreateAsync(token);

        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest(
                "Codex Worker Config",
                "agent-1",
                Code: "codex-config",
                AgentTokenId: token.Id,
                ProjectIds: ["project-1"],
                PollIntervalSeconds: 20,
                MaxRunMinutes: 120,
                WorkspaceRoot: "/data/workspaces",
                RunSmokeOnStartup: true,
                SmokePrompt: "hello",
                CodexProvider: "openai",
                CodexModel: "gpt-5.4",
                OpenAiBaseUrl: "https://api.openai.com/v1"),
            "admin");

        var config = await runtime.GetRuntimeConfigAsync(worker.Code);

        Assert.Equal(worker.Id, config.WorkerId);
        Assert.Equal("codex-config", config.WorkerCode);
        Assert.Equal("/data/workspaces", config.WorkspaceRoot);
        Assert.Equal(20, config.PollIntervalSeconds);
        Assert.Equal(120, config.MaxRunMinutes);
        Assert.True(config.RunSmokeOnStartup);
        Assert.Equal("hello", config.SmokePrompt);
        Assert.Equal("gpt-platform", config.CodexModel);
        Assert.Equal("https://platform.example/v1", config.OpenAiBaseUrl);
        Assert.Equal("platform-api-key", config.OpenAiApiKey);
        Assert.Equal("agent-token-value", config.AgentToken);
    }

    [Fact]
    public async Task Runtime_GetRuntimeConfigByAgentToken_ResolvesBoundWorkerProfile()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var rawToken = new string('a', 64);
        var token = new AgentTokenEntity
        {
            Name = "worker-token",
            TokenHash = HashToken(rawToken),
            TokenValue = rawToken,
            OwnerUserId = "agent-1",
            CreatedBy = "admin",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Status = 1
        };
        await domains.AgentTokens.CreateAsync(token);
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest(
                "Math Worker",
                "agent-1",
                Code: "math-codex-worker-1",
                AgentTokenId: token.Id,
                PollIntervalSeconds: 25,
                MaxRunMinutes: 15,
                SmokePrompt: "Hello from AgentSprint Worker smoke."),
            "admin");

        var config = await runtime.GetRuntimeConfigByAgentTokenAsync(rawToken);

        Assert.Equal(worker.Id, config.WorkerId);
        Assert.Equal("math-codex-worker-1", config.WorkerCode);
        Assert.Equal(25, config.PollIntervalSeconds);
        Assert.Equal(15, config.MaxRunMinutes);
        Assert.Equal(rawToken, config.AgentToken);
        Assert.NotNull((await domains.AgentTokens.GetAsync(token.Id))?.LastUsedAt);
    }

    [Fact]
    public async Task Runtime_GetRuntimeConfigByAgentToken_RejectsUnboundToken()
    {
        var domains = new DigitalWorkerTestDomains();
        var runtime = domains.CreateRuntimeService();
        var rawToken = new string('b', 64);
        await domains.AgentTokens.CreateAsync(new AgentTokenEntity
        {
            Name = "unbound",
            TokenHash = HashToken(rawToken),
            TokenValue = rawToken,
            OwnerUserId = "agent-1",
            CreatedBy = "admin",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Status = 1
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.GetRuntimeConfigByAgentTokenAsync(rawToken));
    }

    [Fact]
    public async Task Management_CreateWorker_RejectsDuplicateRequestedCode()
    {
        var domains = new DigitalWorkerTestDomains();
        var service = domains.CreateManagementService();

        await service.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker", "agent-1", Code: "codex-1"),
            "admin");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWorkerAsync(
                new CreateDigitalWorkerRequest("Duplicated", "agent-2", Code: "codex-1"),
                "admin"));
    }

    [Fact]
    public async Task Runtime_RegisterHeartbeatAndCommand_ReturnsPendingCommandAndAcksOnce()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker 2", "agent-1", Code: "codex-2"),
            "admin");
        var command = await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.Smoke, "{\"prompt\":\"hi\"}", Title: "Manual smoke"),
            "admin");

        var session = await runtime.RegisterSessionAsync(
            new RegisterWorkerSessionRequest(
                worker.Id,
                "instance-1",
                HostName: "worker-host",
                CodexVersion: "codex 1.0.0",
                ConfigTomlExists: true,
                WorkspaceRoot: "/workspaces"));
        var heartbeat = await runtime.HeartbeatAsync(
            new WorkerHeartbeatRequest(worker.Id, session.Id, WorkerSessionStatuses.Idle));
        var acked = await runtime.AckCommandAsync(command.Id, new AckWorkerCommandRequest(session.Id));
        var heartbeatAfterAck = await runtime.HeartbeatAsync(
            new WorkerHeartbeatRequest(worker.Id, session.Id, WorkerSessionStatuses.Idle));

        Assert.Single(heartbeat.Commands, item => item.Id == command.Id);
        Assert.Equal("Manual smoke", command.Title);
        Assert.Equal(WorkerCommandStatuses.Acked, acked.Status);
        Assert.Equal(session.Id, acked.SessionId);
        Assert.Empty(heartbeatAfterAck.Commands);
    }

    [Fact]
    public async Task Runtime_RunLifecycle_UpdatesSessionCommandAndEventTimeline()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker 3", "agent-1", Code: "codex-3"),
            "admin");
        var session = await runtime.RegisterSessionAsync(
            new RegisterWorkerSessionRequest(worker.Id, "instance-1"));
        var command = await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.Smoke),
            "admin");
        await runtime.StartCommandAsync(command.Id, new AckWorkerCommandRequest(session.Id));

        var run = await runtime.StartRunAsync(
            new StartWorkerRunRequest(
                worker.Id,
                session.Id,
                WorkerRunTypes.Smoke,
                WorkerRunStatuses.Running,
                CommandId: command.Id,
                WorkspacePath: "/workspaces/_smoke"));
        var finished = await runtime.FinishRunAsync(
            run.Id,
            new FinishWorkerRunRequest(
                WorkerRunStatuses.Success,
                ExitCode: 0,
                ResultJson: "{\"ok\":true}",
                ChangedFilesJson: "[{\"path\":\"README.md\",\"status\":\"modified\"}]",
                GitCommitId: "abc123"));
        var detail = await management.GetWorkerDetailAsync(worker.Id);
        var commands = await domains.Commands.ListAsync(entity => entity.Id == command.Id);
        var events = await management.ListEventsAsync(worker.Id);
        var completedCommand = Assert.Single(commands);

        Assert.Equal(WorkerRunStatuses.Success, finished.Status);
        Assert.Null(detail.LatestSession?.CurrentRunId);
        Assert.Equal(WorkerSessionStatuses.Idle, detail.LatestSession?.Status);
        Assert.Equal(WorkerCommandStatuses.Succeeded, completedCommand.Status);
        Assert.Equal("[{\"path\":\"README.md\",\"status\":\"modified\"}]", completedCommand.ChangedFilesJson);
        Assert.Equal("abc123", completedCommand.GitCommitId);
        Assert.Contains(events, item => item.EventType == "codex_started");
        Assert.Contains(events, item => item.EventType == "codex_finished");
    }

    [Fact]
    public async Task Runtime_AppendCommandLog_BuffersChunksPersistsOnCompletedAndKeepsLatestTwoHundred()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker Log", "agent-1", Code: "codex-log"),
            "admin");
        var session = await runtime.RegisterSessionAsync(
            new RegisterWorkerSessionRequest(worker.Id, "instance-1"));

        for (var index = 0; index < 201; index++)
        {
            var command = await management.CreateCommandAsync(
                new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.Smoke),
                "admin");
            await runtime.StartCommandAsync(command.Id, new AckWorkerCommandRequest(session.Id));
            var run = await runtime.StartRunAsync(
                new StartWorkerRunRequest(
                    worker.Id,
                    session.Id,
                    WorkerRunTypes.Smoke,
                    WorkerRunStatuses.Running,
                    CommandId: command.Id));

            await runtime.AppendCommandLogAsync(
                worker.Id,
                new AppendWorkerCommandLogRequest(
                    command.Id,
                    $"line-{index}\n",
                    session.Id,
                    run.Id,
                    "instance-1",
                    Sequence: 1));
            var completed = await runtime.AppendCommandLogAsync(
                worker.Id,
                new AppendWorkerCommandLogRequest(
                    command.Id,
                    null,
                    session.Id,
                    run.Id,
                    "instance-1",
                    Sequence: 2,
                    Completed: true));

            Assert.True(completed.Completed);
        }

        var activeLogs = await domains.CommandLogs.ListAsync(entity => entity.WorkerId == worker.Id);
        var deletedLogs = (await domains.CommandLogs.ListIncludingDeletedAsync(entity => entity.WorkerId == worker.Id))
            .Where(entity => entity.IsDelete == 1)
            .ToList();
        var latestLog = activeLogs.OrderByDescending(entity => entity.CreateTime).First();

        Assert.Equal(200, activeLogs.Count);
        Assert.Single(deletedLogs);
        Assert.Contains("line-200", latestLog.LogText);
        Assert.Null(domains.CommandLogBuffer.Get(latestLog.CommandId));
    }

    [Fact]
    public async Task Runtime_AppendCommandLog_CreatesCompletedLogWithoutUpdatingNewEntity()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker Log", "agent-1", Code: "codex-log"),
            "admin");
        var session = await runtime.RegisterSessionAsync(
            new RegisterWorkerSessionRequest(worker.Id, "instance-1"));
        var command = await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.Smoke),
            "admin");
        await runtime.StartCommandAsync(command.Id, new AckWorkerCommandRequest(session.Id));
        var run = await runtime.StartRunAsync(
            new StartWorkerRunRequest(
                worker.Id,
                session.Id,
                WorkerRunTypes.Smoke,
                WorkerRunStatuses.Running,
                CommandId: command.Id));

        await runtime.AppendCommandLogAsync(
            worker.Id,
            new AppendWorkerCommandLogRequest(
                command.Id,
                "done\n",
                session.Id,
                run.Id,
                "instance-1",
                Sequence: 1,
                Completed: true));

        Assert.Equal(1, domains.CommandLogs.CreateCount);
        Assert.Equal(0, domains.CommandLogs.UpdateCount);
        var log = Assert.Single(await domains.CommandLogs.ListAsync(entity => entity.CommandId == command.Id));
        Assert.Equal("done\n", log.LogText);
        Assert.Equal(run.Id, log.RunId);
        Assert.Null(domains.CommandLogBuffer.Get(command.Id));
    }

    [Fact]
    public async Task Runtime_AppendCommandLog_UpdatesExistingCompletedLog()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker Log", "agent-1", Code: "codex-log"),
            "admin");
        var session = await runtime.RegisterSessionAsync(
            new RegisterWorkerSessionRequest(worker.Id, "instance-1"));
        var command = await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.Smoke),
            "admin");
        await runtime.StartCommandAsync(command.Id, new AckWorkerCommandRequest(session.Id));
        await domains.CommandLogs.CreateAsync(new WorkerCommandLogEntity
        {
            WorkerId = worker.Id,
            CommandId = command.Id,
            InstanceId = "instance-1",
            LogText = "old"
        });
        var run = await runtime.StartRunAsync(
            new StartWorkerRunRequest(
                worker.Id,
                session.Id,
                WorkerRunTypes.Smoke,
                WorkerRunStatuses.Running,
                CommandId: command.Id));

        await runtime.AppendCommandLogAsync(
            worker.Id,
            new AppendWorkerCommandLogRequest(
                command.Id,
                "new\n",
                session.Id,
                run.Id,
                "instance-1",
                Sequence: 1,
                Completed: true));

        Assert.Equal(1, domains.CommandLogs.CreateCount);
        Assert.Equal(1, domains.CommandLogs.UpdateCount);
        var log = Assert.Single(await domains.CommandLogs.ListAsync(entity => entity.CommandId == command.Id));
        Assert.Equal("new\n", log.LogText);
        Assert.Equal(run.Id, log.RunId);
        Assert.NotNull(log.UpdateTime);
    }

    [Fact]
    public async Task Runtime_WorkPrompt_RendersDigitalWorkerTemplateAndCompletesTask()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var account = new GitAccountEntity
        {
            Code = "MAIN",
            Name = "Main Git",
            Username = "codex",
            AccessToken = "secret",
            CommitAuthorName = "AgentSprint Bot",
            CommitAuthorEmail = "agentsprint-bot@example.com",
            Status = GitAccountStatuses.Active,
            CreatedBy = "admin"
        };
        await domains.GitAccounts.CreateAsync(account);
        var repository = new GitRepositoryEntity
        {
            Code = "MATH",
            Name = "Math Repository",
            RepositoryUrl = "https://example.com/math.git",
            DefaultBranch = "main",
            GitAccountId = account.Id,
            Status = GitRepositoryStatuses.Active,
            CreatedBy = "admin"
        };
        await domains.GitRepositories.CreateAsync(repository);
        var project = new SprintProjectEntity
        {
            Code = "math",
            Name = "Math Platform",
            GitRepositoryId = repository.Id,
            CreatedBy = "pm"
        };
        await domains.Projects.CreateAsync(project);
        var requirement = new SprintRequirementEntity
        {
            ProjectId = project.Id,
            Title = "Calculate fractions",
            Description = "Users need fraction addition.",
            Status = SprintRequirementStatuses.Developing,
            CreatedBy = "pm"
        };
        await domains.Requirements.CreateAsync(requirement);
        var task = new SprintDevelopmentTaskEntity
        {
            ProjectId = project.Id,
            RequirementId = requirement.Id,
            Title = "Implement fraction addition",
            Description = "Add API and tests.",
            Status = SprintDevelopmentTaskStatuses.InProgress,
            AssigneeId = "agent-1",
            CreatedBy = "pm"
        };
        await domains.Tasks.CreateAsync(task);
        await domains.PromptTemplates.CreateAsync(new PromptTemplateEntity
        {
            AgentEnvironment = "codex",
            Code = "digital_worker_task_execution",
            Name = "Digital Worker",
            Content = "Task {{taskTitle}} for {{projectCode}}. Workspace root {{workspaceRoot}}. Runs {{runsRoot}}. Codex home {{codexHome}}. Model {{codexModel}}. Requirement: {{requirementDescription}}. {{completionInstruction}}"
        });
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest(
                "Codex Worker",
                "agent-1",
                Code: "codex-prompt",
                ProjectIds: [project.Id],
                WorkspaceRoot: "/data/workspaces",
                RunsRoot: "/data/runs",
                CodexHome: "/data/codex-home",
                CodexModel: "gpt-5.4"),
            "admin");

        var prompt = await runtime.GetWorkPromptAsync(worker.Id, WorkerRunTypes.Task, task.Id);
        var completed = await runtime.CompleteWorkAsync(worker.Id, WorkerRunTypes.Task, task.Id);

        Assert.Equal("digital_worker_task_execution", prompt.TemplateCode);
        Assert.Contains("Implement fraction addition", prompt.Prompt);
        Assert.Contains("Users need fraction addition.", prompt.Prompt);
        Assert.Contains("Digital worker runtime context:", prompt.Prompt);
        Assert.Contains("- Workspace root: /data/workspaces", prompt.Prompt);
        Assert.Contains("- Current workspace path: /data/workspaces/math", prompt.Prompt);
        Assert.Contains("- Runs/log root: /data/runs", prompt.Prompt);
        Assert.Contains("- Codex Home: /data/codex-home", prompt.Prompt);
        Assert.Contains("Workspace root /data/workspaces.", prompt.Prompt);
        Assert.Contains("Runs /data/runs.", prompt.Prompt);
        Assert.Contains("Codex home /data/codex-home.", prompt.Prompt);
        Assert.Contains("Model gpt-5.4.", prompt.Prompt);
        Assert.DoesNotContain("secret", prompt.Prompt);
        Assert.DoesNotContain("agentsprint.get_task_prompt", prompt.Prompt);
        Assert.Equal("/data/workspaces/math", prompt.Context.WorkspacePath);
        Assert.Equal("https://example.com/math.git", prompt.Context.RepositoryReference);
        Assert.Equal("https://example.com/math.git", prompt.Context.RepositoryUrl);
        Assert.Equal(repository.Id, prompt.Context.GitRepositoryId);
        Assert.Equal(account.Id, prompt.Context.GitAccountId);
        Assert.Equal("main", prompt.Context.RepositoryDefaultBranch);
        Assert.Equal("codex", prompt.Context.GitUsername);
        Assert.Equal("secret", prompt.Context.GitAccessToken);
        Assert.Equal("AgentSprint Bot", prompt.Context.GitCommitAuthorName);
        Assert.Equal("agentsprint-bot@example.com", prompt.Context.GitCommitAuthorEmail);
        Assert.Equal($"/worker-runtime/work/task/{task.Id}/complete", prompt.Context.CompletionApiPath);
        Assert.Equal(SprintDevelopmentTaskStatuses.Completed, completed.Status);
        Assert.Equal(SprintDevelopmentTaskStatuses.Completed, (await domains.Tasks.GetAsync(task.Id))?.Status);
    }

    [Fact]
    public async Task Runtime_StartRun_MarksAssignedTaskAndRequirementInProgress()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var project = new SprintProjectEntity
        {
            Code = "task-start",
            Name = "Task start project",
            CreatedBy = "pm"
        };
        await domains.Projects.CreateAsync(project);
        var requirement = new SprintRequirementEntity
        {
            ProjectId = project.Id,
            Title = "Start only when worker runs",
            Status = SprintRequirementStatuses.Decomposed,
            CreatedBy = "pm"
        };
        await domains.Requirements.CreateAsync(requirement);
        var task = new SprintDevelopmentTaskEntity
        {
            ProjectId = project.Id,
            RequirementId = requirement.Id,
            Title = "Run codex",
            Status = SprintDevelopmentTaskStatuses.Assigned,
            AssigneeId = "agent-1",
            CreatedBy = "pm"
        };
        await domains.Tasks.CreateAsync(task);
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Task Worker", "agent-1", Code: "codex-task-start", ProjectIds: [project.Id]),
            "admin");
        var session = await runtime.RegisterSessionAsync(
            new RegisterWorkerSessionRequest(worker.Id, "instance-1"));

        await runtime.StartRunAsync(
            new StartWorkerRunRequest(
                worker.Id,
                session.Id,
                WorkerRunTypes.Task,
                WorkerRunStatuses.Running,
                TargetType: WorkerRunTypes.Task,
                TargetId: task.Id));
        var startedTask = await domains.Tasks.GetAsync(task.Id);
        var startedRequirement = await domains.Requirements.GetAsync(requirement.Id);

        Assert.Equal(SprintDevelopmentTaskStatuses.InProgress, startedTask?.Status);
        Assert.NotNull(startedTask?.StartedAt);
        Assert.Equal(SprintRequirementStatuses.Developing, startedRequirement?.Status);
        Assert.Equal("agent-1", startedRequirement?.DeveloperId);
    }

    [Fact]
    public async Task Runtime_HeartbeatDoesNotDispatchWorkWhenAuthRequired()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker", "agent-1", Code: "codex-auth"),
            "admin");
        var session = await runtime.RegisterSessionAsync(new RegisterWorkerSessionRequest(worker.Id, "instance-1"));
        await domains.Tasks.CreateAsync(new SprintDevelopmentTaskEntity
        {
            Id = "task-1",
            ProjectId = "project-1",
            RequirementId = "requirement-1",
            Title = "Auth required task",
            Status = SprintDevelopmentTaskStatuses.Assigned,
            AssigneeId = "agent-1",
            CreatedBy = "pm"
        });
        await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.StartTask, "{\"taskId\":\"task-1\"}"),
            "admin");

        var heartbeat = await runtime.HeartbeatAsync(
            new WorkerHeartbeatRequest(worker.Id, session.Id, WorkerSessionStatuses.AuthRequired));

        Assert.Empty(heartbeat.Commands);
    }

    [Fact]
    public async Task Management_CreateStartTaskCommand_RejectsTaskAssignedToAnotherUser()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker", "agent-1", Code: "codex-wrong-assignee"),
            "admin");
        var task = new SprintDevelopmentTaskEntity
        {
            ProjectId = "project-1",
            RequirementId = "requirement-1",
            Title = "Assigned elsewhere",
            Description = "Should not be dispatched.",
            Status = SprintDevelopmentTaskStatuses.Assigned,
            AssigneeId = "agent-2",
            CreatedBy = "pm"
        };
        await domains.Tasks.CreateAsync(task);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            management.CreateCommandAsync(
                new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.StartTask, $$"""{"taskId":"{{task.Id}}"}"""),
                "admin"));

        Assert.Equal("Target is assigned to another user.", error.Message);
        Assert.Empty(await management.ListCommandsAsync(worker.Id));
    }

    [Fact]
    public async Task Management_CreateStartTaskCommand_AllowsTaskAssignedToWorkerAgentUser()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker", "agent-1", Code: "codex-right-assignee"),
            "admin");
        var task = new SprintDevelopmentTaskEntity
        {
            ProjectId = "project-1",
            RequirementId = "requirement-1",
            Title = "Assigned to worker",
            Description = "Should be dispatched.",
            Status = SprintDevelopmentTaskStatuses.Assigned,
            AssigneeId = "agent-1",
            CreatedBy = "pm"
        };
        await domains.Tasks.CreateAsync(task);

        var command = await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.StartTask, $$"""{"taskId":"{{task.Id}}"}"""),
            "admin");

        Assert.Equal(worker.Id, command.WorkerId);
        Assert.Equal(WorkerCommandTypes.StartTask, command.CommandType);
        Assert.Equal(task.Title, command.Title);
    }

    [Fact]
    public async Task Runtime_HeartbeatDispatchesControlCommandsWhileBusy()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker", "agent-1", Code: "codex-busy"),
            "admin");
        var session = await runtime.RegisterSessionAsync(new RegisterWorkerSessionRequest(worker.Id, "instance-1"));
        await domains.Tasks.CreateAsync(new SprintDevelopmentTaskEntity
        {
            Id = "task-1",
            ProjectId = "project-1",
            RequirementId = "requirement-1",
            Title = "Busy task",
            Status = SprintDevelopmentTaskStatuses.Assigned,
            AssigneeId = "agent-1",
            CreatedBy = "pm"
        });
        await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.StartTask, "{\"taskId\":\"task-1\"}"),
            "admin");
        await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.CancelCurrentRun),
            "admin");

        var heartbeat = await runtime.HeartbeatAsync(
            new WorkerHeartbeatRequest(worker.Id, session.Id, WorkerSessionStatuses.Busy, "run-1"));

        var command = Assert.Single(heartbeat.Commands);
        Assert.Equal(WorkerCommandTypes.CancelCurrentRun, command.CommandType);
    }

    [Fact]
    public async Task Management_Queries_AcceptWorkerCodeAsFilter()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker", "agent-1", Code: "codex-code"),
            "admin");
        var session = await runtime.RegisterSessionAsync(
            new RegisterWorkerSessionRequest(worker.Code, "instance-1"));
        var command = await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Code, WorkerCommandTypes.Smoke),
            "admin");
        await runtime.StartCommandAsync(command.Id, new AckWorkerCommandRequest(session.Id));
        var run = await runtime.StartRunAsync(
            new StartWorkerRunRequest(
                worker.Code,
                session.Id,
                WorkerRunTypes.Smoke,
                WorkerRunStatuses.Running,
                CommandId: command.Id));
        await runtime.ReportEventAsync(
            new ReportWorkerEventRequest(
                worker.Code,
                "worker_test_event",
                "Worker test event.",
                session.Id,
                run.Id));
        await runtime.ReportEventAsync(
            new ReportWorkerEventRequest(
                worker.Code,
                "codex_idle_timeout",
                "Codex process reached stdout/stderr idle timeout.",
                session.Id,
                run.Id,
                Level: "error",
                PayloadJson: "{\"localRunId\":\"smoke-1\",\"hasOutput\":false}"));

        var detail = await management.GetWorkerDetailAsync(worker.Code);
        var sessions = await management.ListSessionsAsync(worker.Code);
        var runs = await management.ListRunsAsync(worker.Code);
        var events = await management.ListEventsAsync(worker.Code);

        Assert.Equal(session.Id, detail.LatestSession?.Id);
        Assert.Equal(run.Id, detail.CurrentRun?.Id);
        Assert.Equal(worker.Id, Assert.Single(sessions).WorkerId);
        Assert.Equal(run.Id, Assert.Single(runs).Id);
        Assert.Contains(events, item => item.EventType == "worker_test_event");
        Assert.Contains(events, item =>
            item.EventType == "codex_idle_timeout" &&
            item.Level == "error" &&
            item.PayloadJson?.Contains("\"hasOutput\":false", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Management_ListCommands_FiltersByWorkerCodeAndStatus()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker", "agent-1", Code: "codex-command-audit"),
            "admin");
        var otherWorker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Other Worker", "agent-1", Code: "codex-command-other"),
            "admin");
        var command = await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.Smoke, "{\"prompt\":\"hi\"}"),
            "admin");
        await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(otherWorker.Id, WorkerCommandTypes.ReloadConfig),
            "admin");
        var session = await runtime.RegisterSessionAsync(
            new RegisterWorkerSessionRequest(worker.Id, "instance-1"));
        await runtime.AckCommandAsync(command.Id, new AckWorkerCommandRequest(session.Id));

        var commands = await management.ListCommandsAsync(worker.Code, status: WorkerCommandStatuses.Acked);

        var audited = Assert.Single(commands);
        Assert.Equal(command.Id, audited.Id);
        Assert.Equal(WorkerCommandTypes.Smoke, audited.CommandType);
        Assert.Equal("{\"prompt\":\"hi\"}", audited.PayloadJson);
    }

    [Fact]
    public async Task Management_ReplayCommand_CopiesCommandTypeAndPayloadAsPending()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker", "agent-1", Code: "codex-command-replay"),
            "admin");
        await domains.Tasks.CreateAsync(new SprintDevelopmentTaskEntity
        {
            Id = "task-1",
            ProjectId = "project-1",
            RequirementId = "requirement-1",
            Title = "Replay task",
            Status = SprintDevelopmentTaskStatuses.Assigned,
            AssigneeId = "agent-1",
            CreatedBy = "pm"
        });
        var source = await management.CreateCommandAsync(
            new CreateWorkerCommandRequest(worker.Id, WorkerCommandTypes.StartTask, "{\"taskId\":\"task-1\"}"),
            "admin");

        var replayed = await management.ReplayCommandAsync(source.Id, "auditor");
        var commands = await management.ListCommandsAsync(worker.Id);

        Assert.Equal(worker.Id, replayed.WorkerId);
        Assert.Equal(WorkerCommandTypes.StartTask, replayed.CommandType);
        Assert.Equal("{\"taskId\":\"task-1\"}", replayed.PayloadJson);
        Assert.Equal(WorkerCommandStatuses.Pending, replayed.Status);
        Assert.Null(replayed.SessionId);
        Assert.Equal("auditor", replayed.CreatedBy);
        Assert.Contains(commands, item => item.Id == source.Id);
        Assert.Contains(commands, item => item.Id == replayed.Id);
    }

    [Fact]
    public async Task Runtime_RegisterSession_RejectsDisabledWorker()
    {
        var domains = new DigitalWorkerTestDomains();
        var management = domains.CreateManagementService();
        var runtime = domains.CreateRuntimeService();
        var worker = await management.CreateWorkerAsync(
            new CreateDigitalWorkerRequest("Codex Worker 4", "agent-1", Code: "codex-4"),
            "admin");
        await management.SetWorkerStatusAsync(
            worker.Id,
            new SetDigitalWorkerStatusRequest(DigitalWorkerStatuses.Disabled));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.RegisterSessionAsync(new RegisterWorkerSessionRequest(worker.Id, "instance-1")));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

internal sealed class MutableSystemConfigurationService : ISystemConfigurationService
{
    private AiPlatformRuntimeResult? _aiPlatform;

    public void SetAiPlatform(AiPlatformRuntimeResult aiPlatform)
    {
        _aiPlatform = aiPlatform;
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
        var result = new AiPlatformResult(
            request.Id ?? Guid.NewGuid().ToString("N"),
            request.Code,
            request.Name,
            request.Provider,
            request.Model,
            !string.IsNullOrWhiteSpace(request.ApiKey),
            request.OpenAiBaseUrl,
            request.Description,
            request.Sort,
            request.Status);
        _aiPlatform = new AiPlatformRuntimeResult(
            result.Code,
            result.Name,
            result.Provider,
            result.Model,
            request.ApiKey,
            result.OpenAiBaseUrl,
            result.Status);
        return Task.FromResult(result);
    }

    public Task<AiPlatformRuntimeResult?> GetAiPlatformRuntimeAsync(string code)
    {
        return Task.FromResult(
            string.Equals(_aiPlatform?.Code, code, StringComparison.OrdinalIgnoreCase)
                ? _aiPlatform
                : null);
    }

    public Task<bool> DeleteAiPlatformAsync(string id)
    {
        return Task.FromResult(true);
    }

    public Task<string> GetValueAsync(string key, string defaultValue)
    {
        return Task.FromResult(defaultValue);
    }
}

internal sealed class DigitalWorkerTestDomains
{
    public InMemoryDigitalWorkerDomain Workers { get; } = new();

    public InMemoryWorkerSessionDomain Sessions { get; } = new();

    public InMemoryWorkerCommandDomain Commands { get; } = new();

    public InMemoryWorkerCommandLogDomain CommandLogs { get; } = new();

    public InMemoryWorkerCommandLogBuffer CommandLogBuffer { get; } = new();

    public InMemoryWorkerRunDomain Runs { get; } = new();

    public InMemoryWorkerEventDomain Events { get; } = new();

    public InMemoryDigitalWorkerSprintProjectDomain Projects { get; } = new();

    public InMemoryDigitalWorkerGitRepositoryDomain GitRepositories { get; } = new();

    public InMemoryDigitalWorkerGitAccountDomain GitAccounts { get; } = new();

    public InMemoryDigitalWorkerSprintRequirementDomain Requirements { get; } = new();

    public InMemoryDigitalWorkerSprintDevelopmentTaskDomain Tasks { get; } = new();

    public InMemoryDigitalWorkerSprintBugDomain Bugs { get; } = new();

    public InMemoryDigitalWorkerSprintSkillDomain Skills { get; } = new();

    public InMemoryPromptTemplateDomain PromptTemplates { get; } = new();

    private readonly List<AgentTokenEntity> _agentTokens = [];

    public InMemoryAgentTokenDomain AgentTokens { get; }

    public MutableSystemConfigurationService ConfigurationService { get; } = new();

    public DigitalWorkerTestDomains()
    {
        AgentTokens = new InMemoryAgentTokenDomain(_agentTokens);
    }

    public DigitalWorkerManagementService CreateManagementService()
    {
        return new DigitalWorkerManagementService(Workers, Sessions, Commands, CommandLogs, CommandLogBuffer, Runs, Events, Tasks, Bugs);
    }

    public DigitalWorkerRuntimeService CreateRuntimeService()
    {
        return new DigitalWorkerRuntimeService(
            Workers,
            AgentTokens,
            ConfigurationService,
            Projects,
            GitRepositories,
            GitAccounts,
            Requirements,
            Tasks,
            Bugs,
            Skills,
            PromptTemplates,
            Sessions,
            Commands,
            CommandLogs,
            CommandLogBuffer,
            Runs,
            Events);
    }
}

internal sealed class InMemoryDigitalWorkerDomain :
    InMemoryDigitalWorkerDomainBase<DigitalWorkerEntity>,
    IDigitalWorkerDomain;

internal sealed class InMemoryWorkerSessionDomain :
    InMemoryDigitalWorkerDomainBase<WorkerSessionEntity>,
    IWorkerSessionDomain;

internal sealed class InMemoryWorkerCommandDomain :
    InMemoryDigitalWorkerDomainBase<WorkerCommandEntity>,
    IWorkerCommandDomain;

internal sealed class InMemoryWorkerCommandLogDomain :
    InMemoryDigitalWorkerDomainBase<WorkerCommandLogEntity>,
    IWorkerCommandLogDomain;

internal sealed class InMemoryWorkerCommandLogBuffer : IWorkerCommandLogBuffer
{
    private readonly Dictionary<string, WorkerCommandLogSnapshotResult> _snapshots = new(StringComparer.Ordinal);

    public WorkerCommandLogSnapshotResult Append(
        string workerId,
        string commandId,
        string? sessionId,
        string? runId,
        string instanceId,
        string? chunk,
        long sequence,
        bool completed)
    {
        _snapshots.TryGetValue(commandId, out var existing);
        var snapshot = new WorkerCommandLogSnapshotResult(
            commandId,
            runId ?? existing?.RunId,
            sessionId ?? existing?.SessionId,
            instanceId,
            (existing?.LogText ?? string.Empty) + (chunk ?? string.Empty),
            sequence,
            completed,
            DateTime.UtcNow);
        _snapshots[commandId] = snapshot;
        return snapshot;
    }

    public WorkerCommandLogSnapshotResult? Get(string commandId)
    {
        return _snapshots.TryGetValue(commandId, out var snapshot) ? snapshot : null;
    }

    public void Remove(string commandId)
    {
        _snapshots.Remove(commandId);
    }
}

internal sealed class InMemoryWorkerRunDomain :
    InMemoryDigitalWorkerDomainBase<WorkerRunEntity>,
    IWorkerRunDomain;

internal sealed class InMemoryWorkerEventDomain :
    InMemoryDigitalWorkerDomainBase<WorkerEventEntity>,
    IWorkerEventDomain;

internal sealed class InMemoryDigitalWorkerSprintProjectDomain :
    InMemoryDigitalWorkerDomainBase<SprintProjectEntity>,
    ISprintProjectDomain;

internal sealed class InMemoryDigitalWorkerGitRepositoryDomain :
    InMemoryDigitalWorkerDomainBase<GitRepositoryEntity>,
    IGitRepositoryDomain;

internal sealed class InMemoryDigitalWorkerGitAccountDomain :
    InMemoryDigitalWorkerDomainBase<GitAccountEntity>,
    IGitAccountDomain;

internal sealed class InMemoryDigitalWorkerSprintRequirementDomain :
    InMemoryDigitalWorkerDomainBase<SprintRequirementEntity>,
    ISprintRequirementDomain;

internal sealed class InMemoryDigitalWorkerSprintDevelopmentTaskDomain :
    InMemoryDigitalWorkerDomainBase<SprintDevelopmentTaskEntity>,
    ISprintDevelopmentTaskDomain;

internal sealed class InMemoryDigitalWorkerSprintBugDomain :
    InMemoryDigitalWorkerDomainBase<SprintBugEntity>,
    ISprintBugDomain;

internal sealed class InMemoryDigitalWorkerSprintSkillDomain :
    InMemoryDigitalWorkerDomainBase<SprintSkillEntity>,
    ISprintSkillDomain;

internal sealed class InMemoryPromptTemplateDomain :
    InMemoryDigitalWorkerDomainBase<PromptTemplateEntity>,
    IPromptTemplateDomain;

internal abstract class InMemoryDigitalWorkerDomainBase<TEntity>
    where TEntity : AgentSprint.Model.Modules.Common.EntityBase, new()
{
    private readonly List<TEntity> _entities = [];

    public int CreateCount { get; private set; }

    public int UpdateCount { get; private set; }

    public Task<string> CreateAsync(TEntity entity)
    {
        CreateCount++;
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
        UpdateCount++;
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
