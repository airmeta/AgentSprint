using AgentSprint.Worker;
using AgentSprint.Worker.Actors;
using AgentSprint.Worker.Models;
using AgentSprint.Worker.Options;
using AgentSprint.Worker.Services;

using Air.Cloud.Modules.Akka.Abstractions;
using Air.Cloud.Modules.Akka.Extensions;
using Air.Cloud.Modules.Akka.Hosting;
using Air.Cloud.Modules.Akka.Options;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using WorkerCommandLogChunkMessage = AgentSprint.Model.Modules.Agile.Workers.WorkerCommandLogChunkMessage;
using WorkerPlatformActorNames = AgentSprint.Model.Modules.Agile.Workers.WorkerPlatformActorNames;

namespace AgentSprint.Tests;

public sealed class WorkerProbeTests
{
    [Fact]
    public async Task WorkerRunLogger_PrepareAndManifest_WritesExpectedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-worker-tests", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new WorkerOptions
        {
            RunsRoot = root
        });
        var logger = new WorkerRunLogger(options);

        var expectedPaths = logger.ResolvePaths("run-001");
        var paths = await logger.PrepareAsync("run-001", "hello", CancellationToken.None);
        await logger.WriteManifestAsync(
            paths,
            new()
            {
                RunId = "run-001",
                Status = "success",
                PromptPath = paths.PromptPath,
                StdoutPath = paths.StdoutPath,
                StderrPath = paths.StderrPath,
                FinalPath = paths.FinalPath,
                StartedAt = DateTimeOffset.UnixEpoch,
                CompletedAt = DateTimeOffset.UnixEpoch
            },
            CancellationToken.None);

        Assert.Equal(expectedPaths, paths);
        Assert.True(File.Exists(paths.PromptPath));
        Assert.True(File.Exists(paths.ManifestPath));
        Assert.Equal("hello", await File.ReadAllTextAsync(paths.PromptPath));
        Assert.Contains("\"status\": \"success\"", await File.ReadAllTextAsync(paths.ManifestPath));
    }

    [Fact]
    public void WorkerOptions_Defaults_MatchProbeServiceMvp()
    {
        var options = new WorkerOptions();

        Assert.Equal("worker-1", options.WorkerId);
        Assert.Equal("/workspaces", options.WorkspaceRoot);
        Assert.Equal("/runs", options.RunsRoot);
        Assert.Equal("/codex-home", options.CodexHome);
        Assert.Equal("workspace-write", options.SandboxMode);
        Assert.False(options.RunSmokeOnStartup);
    }

    [Fact]
    public void WorkerActorNames_EventReporterRegisteredName_UsesConfiguredDomainPrefix()
    {
        Assert.Equal("AgentSprintWorker", WorkerActorNames.Domain);
        Assert.Equal("agentsprint-worker", WorkerActorNames.Role);
        Assert.Equal("event-reporter", WorkerActorNames.EventReporter);
        Assert.Equal("agentsprint-worker-event-reporter", WorkerActorNames.EventReporterRegisteredName);
        Assert.Equal("akka_cluster_started", WorkerEventTypes.AkkaClusterStarted);
    }

    [Fact]
    public void WorkerPlatformActorNames_CommandLogReceiverRegisteredName_UsesPlatformDomainPrefix()
    {
        var message = new WorkerCommandLogChunkMessage(
            "worker-id",
            "session-id",
            "instance-id",
            "command-id",
            "run-id",
            7,
            "chunk",
            false,
            DateTime.UnixEpoch,
            null);

        Assert.Equal("AgentSprintPlatform", WorkerPlatformActorNames.Domain);
        Assert.Equal("agentsprint-platform", WorkerPlatformActorNames.Role);
        Assert.Equal("worker-command-log-receiver", WorkerPlatformActorNames.WorkerCommandLogReceiver);
        Assert.Equal("agentsprint-platform-worker-command-log-receiver", WorkerPlatformActorNames.WorkerCommandLogReceiverRegisteredName);
        Assert.Equal("instance-id", message.InstanceId);
        Assert.Equal("command-id", message.CommandId);
    }

    [Fact]
    public void WorkerDocs_DescribeSharedAkkaClusterConfiguration()
    {
        var docsPath = Path.Combine(FindRepositoryRoot(), "docs", "数字员工受控端探针服务说明.md");
        var docs = File.ReadAllText(docsPath);

        Assert.Contains("\"SystemName\": \"agentsprint-cluster\"", docs);
        Assert.Contains("\"Host\": \"api\"", docs);
        Assert.Contains("\"SeedNodes\": [ \"akka.tcp://agentsprint-cluster@api:25520\" ]", docs);
        Assert.Contains("AkkaSettings__SystemName: \"agentsprint-cluster\"", docs);
        Assert.Contains("AkkaSettings__SeedNodes__0: \"akka.tcp://agentsprint-cluster@api:25520\"", docs);
        Assert.Contains("math-codex-worker-1", docs);
        Assert.Contains("`AkkaSettings:Host` is left as `0.0.0.0`", docs);
    }

    [Fact]
    public void WorkerStartup_RegistersAkkaClusterBeforeWorkerMainLoop()
    {
        var services = new ServiceCollection();

        new Startup().ConfigureServices(services);

        var hostedServices = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .ToList();
        var dependencyInitializerIndex = hostedServices.FindIndex(descriptor => descriptor.ImplementationType == typeof(WorkerActorDependencyInitializer));
        var akkaIndex = hostedServices.FindIndex(descriptor => descriptor.ImplementationType == typeof(AkkaClusterHostedService));
        var workerIndex = hostedServices.FindIndex(descriptor => descriptor.ImplementationType == typeof(AgentSprintWorkerService));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAkkaClusterService));
        Assert.True(dependencyInitializerIndex >= 0);
        Assert.True(akkaIndex >= 0);
        Assert.True(workerIndex >= 0);
        Assert.True(dependencyInitializerIndex < akkaIndex);
        Assert.True(akkaIndex < workerIndex);
    }

    [Fact]
    public async Task WorkerEventReporterActor_AutoRegistersWithAkkaRuntime()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AgentSprintApiClient(
            new HttpClient(new CapturingHandler("""{"code":0,"data":{"id":"event-1","workerId":"worker-1","sessionId":null,"runId":null,"eventType":"test","level":"info","message":"ok","payloadJson":null,"createdAt":"2026-01-01T00:00:00Z"},"message":"ok"}""")),
            Options.Create(new AgentSprintOptions { ApiBaseUrl = "http://agentsprint.test/" })));
        services.AddHostedService<WorkerActorDependencyInitializer>();
        services.AddAkkaCluster(options =>
        {
            options.SystemName = "agentsprint-worker-tests-" + Guid.NewGuid().ToString("N");
            options.Host = "127.0.0.1";
            options.Port = 0;
            options.Roles.Add(WorkerActorNames.Role);
            options.Domains[WorkerActorNames.Domain] = new AkkaDomainOptions
            {
                Role = WorkerActorNames.Role,
                ActorNamePrefix = WorkerActorNames.ActorNamePrefix,
                AllowCrossDomainMessages = true
            };
        });

        await using var provider = services.BuildServiceProvider();
        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        try
        {
            var registry = provider.GetRequiredService<IAkkaActorRegistry>();

            Assert.Contains(
                registry.GetDescriptors(),
                descriptor => descriptor.ActorName == WorkerActorNames.EventReporterRegisteredName &&
                    descriptor.ActorType == typeof(WorkerEventReporterActor));

            var cluster = provider.GetRequiredService<IAkkaClusterService>();
            var currentNode = cluster.GetCurrentNode();

            Assert.Contains(WorkerActorNames.Role, currentNode.Roles);
            Assert.Contains("akka.tcp://", currentNode.Address);
        }
        finally
        {
            foreach (var hostedService in provider.GetServices<IHostedService>().Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }
        }
    }

    [Fact]
    public void AgentSprintWorkerService_StartTaskCommand_ResolvesApiDrivenTarget()
    {
        var command = new AgentSprint.Worker.Models.WorkerCommandResult(
            "command-1",
            "worker-1",
            null,
            AgentSprint.Worker.Models.WorkerPlatformCommandTypes.StartTask,
            "Task title",
            "{\"task_id\":\"task-001\",\"project_code\":\"math\",\"repository_url\":\"https://example.com/math.git\",\"branch\":\"main\"}",
            "pending");

        var target = AgentSprintWorkerService.ResolveCommandTarget(command);

        Assert.Equal("task", target.RunType);
        Assert.Equal("task", target.TargetType);
        Assert.Equal("task-001", target.TargetId);
        Assert.Equal("math", target.ProjectCode);
        Assert.Equal("https://example.com/math.git", target.RepositoryUrl);
        Assert.Equal("main", target.Branch);
    }

    [Fact]
    public void AgentSprintWorkerService_BuildCodexExecutionPrompt_IncludesRuntimePaths()
    {
        var options = new WorkerOptions
        {
            WorkerId = "worker-id",
            WorkerName = "Worker",
            WorkspaceRoot = "/workspaces",
            RunsRoot = "/runs",
            CodexHome = "/codex-home",
            CodexProvider = "openai",
            CodexModel = "gpt-5.4",
            SandboxMode = "workspace-write",
            CodexExecutable = "codex",
            ConfigVersion = 3
        };
        var snapshot = new WorkerEnvironmentSnapshot(
            new CommandProbeResult("codex", "--version", 0, "codex 1.0.0", string.Empty, false, null),
            new CommandProbeResult("git", "--version", 0, "git 2.0.0", string.Empty, false, null),
            new CommandProbeResult("dotnet", "--version", 0, "10.0.0", string.Empty, false, null),
            new CommandProbeResult("node", "--version", 0, "node 24.0.0", string.Empty, false, null),
            new CommandProbeResult("codex", "login status", 0, "logged in", string.Empty, false, null),
            ConfigTomlExists: true,
            CodexHome: "/codex-home",
            WorkspaceRoot: "/workspaces",
            RunsRoot: "/runs");
        var paths = new RunPaths(
            "/runs/task-1",
            "/runs/task-1/prompt.txt",
            "/runs/task-1/stdout.log",
            "/runs/task-1/stderr.log",
            "/runs/task-1/final.md",
            "/runs/task-1/run.json");
        var target = new AgentSprintWorkerService.WorkerCommandTarget(
            "task",
            "task",
            "task-1",
            "taskId",
            "math",
            null,
            "main",
            "Task");
        var workspace = new WorkspacePreparationResult(
            true,
            "/workspaces/math",
            true,
            "https://example.com/math.git",
            "main",
            "abcdef",
            false,
            null);

        var prompt = AgentSprintWorkerService.BuildCodexExecutionPrompt(
            "Do the task.",
            options,
            snapshot,
            "task-1",
            "/workspaces/math",
            paths,
            target,
            "math",
            workspace);

        Assert.Contains("- Workspace root: /workspaces", prompt);
        Assert.Contains("- Current workspace path: /workspaces/math", prompt);
        Assert.Contains("- Runs/log root: /runs", prompt);
        Assert.Contains("- Stdout log path: /runs/task-1/stdout.log", prompt);
        Assert.Contains("- Stderr log path: /runs/task-1/stderr.log", prompt);
        Assert.Contains("- Codex Home: /codex-home", prompt);
        Assert.Contains("- Codex model: gpt-5.4", prompt);
        Assert.Contains("Do the task.", prompt);
        Assert.DoesNotContain("Agent Token", prompt);
    }

    [Fact]
    public void AgentSprintWorkerService_BuildWorkerCommitMessage_IncludesRequirementContext()
    {
        var target = new AgentSprintWorkerService.WorkerCommandTarget(
            "task",
            "task",
            "task-001",
            "taskId",
            "math",
            null,
            "main",
            "Task");
        var context = new WorkerPromptContextResult(
            "task",
            "task-001",
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
            null,
            null,
            "req-001",
            "Checkout search",
            "Allow users to search orders from the checkout page.",
            "developing",
            null,
            null,
            "task-001",
            "Implement checkout search API",
            "Add endpoint, service logic, and focused tests.",
            null,
            null,
            null,
            null,
            null,
            null,
            "/worker-runtime/work/task/task-001/complete",
            "Worker completes through API.");
        var result = new CodexRunResult(
            "run-001",
            "success",
            0,
            false,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            "/runs/run-001",
            "/runs/run-001/stdout.log",
            "/runs/run-001/stderr.log",
            "/runs/run-001/final.md",
            null);

        var message = AgentSprintWorkerService.BuildWorkerCommitMessage(target, context, result);

        Assert.Contains("AgentSprint worker update: Implement checkout search API", message);
        Assert.Contains("Requirement: req-001 - Checkout search", message);
        Assert.Contains("Requirement summary: Allow users to search orders from the checkout page.", message);
        Assert.Contains("Task: task-001 - Implement checkout search API", message);
        Assert.DoesNotContain("AgentSprint worker update: task task-001\r\n", message);
    }

    [Fact]
    public void GitWorkspaceManager_ResolveWorkspacePath_RejectsEscapingProjectCode()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-worker-tests", Guid.NewGuid().ToString("N"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GitWorkspaceManager.ResolveWorkspacePath(root, ".."));

        Assert.Equal("Project workspace path escapes WorkspaceRoot.", ex.Message);
    }

    [Fact]
    public async Task GitWorkspaceManager_PrepareAsync_ClonesAndPullsLatestCommit()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-worker-tests", Guid.NewGuid().ToString("N"));
        var remote = Path.Combine(root, "remote.git");
        var source = Path.Combine(root, "source");
        var workspaces = Path.Combine(root, "workspaces");
        Directory.CreateDirectory(root);
        await RunGitForTestAsync("init --bare " + Quote(remote), root);
        await RunGitForTestAsync("init -b main " + Quote(source), root);
        await RunGitForTestAsync("config user.email worker@example.com", source);
        await RunGitForTestAsync("config user.name Worker", source);
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "first");
        await RunGitForTestAsync("add README.md", source);
        await RunGitForTestAsync("commit -m first", source);
        await RunGitForTestAsync("remote add origin " + Quote(remote), source);
        await RunGitForTestAsync("push -u origin main", source);

        var manager = new GitWorkspaceManager();
        var cloned = await manager.PrepareAsync(workspaces, "math", remote, "main", null, null, null, null, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "second");
        await RunGitForTestAsync("add README.md", source);
        await RunGitForTestAsync("commit -m second", source);
        await RunGitForTestAsync("push", source);

        var pulled = await manager.PrepareAsync(workspaces, "math", remote, "main", null, null, null, null, CancellationToken.None);

        Assert.True(cloned.Succeeded);
        Assert.True(pulled.Succeeded);
        Assert.True(pulled.RepositoryAvailable);
        Assert.Equal("main", pulled.Branch);
        Assert.False(pulled.Dirty);
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(pulled.WorkspacePath, "README.md")));
    }

    [Fact]
    public async Task GitWorkspaceManager_PrepareAsync_CleansExistingWorkspaceBeforePull()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-worker-tests", Guid.NewGuid().ToString("N"));
        var remote = Path.Combine(root, "remote.git");
        var source = Path.Combine(root, "source");
        var workspaces = Path.Combine(root, "workspaces");
        Directory.CreateDirectory(root);
        await RunGitForTestAsync("init --bare " + Quote(remote), root);
        await RunGitForTestAsync("init -b main " + Quote(source), root);
        await ConfigureGitUserAsync(source);
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "first");
        await RunGitForTestAsync("add README.md", source);
        await RunGitForTestAsync("commit -m first", source);
        await RunGitForTestAsync("remote add origin " + Quote(remote), source);
        await RunGitForTestAsync("push -u origin main", source);

        var manager = new GitWorkspaceManager();
        var prepared = await manager.PrepareAsync(workspaces, "math", remote, "main", null, null, null, null, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(prepared.WorkspacePath, "README.md"), "stale local edit");
        await File.WriteAllTextAsync(Path.Combine(prepared.WorkspacePath, "scratch.txt"), "stale untracked file");

        var cleaned = await manager.PrepareAsync(workspaces, "math", remote, "main", null, null, null, null, CancellationToken.None);

        Assert.True(cleaned.Succeeded);
        Assert.False(cleaned.Dirty);
        Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(cleaned.WorkspacePath, "README.md")));
        Assert.False(File.Exists(Path.Combine(cleaned.WorkspacePath, "scratch.txt")));
    }

    [Fact]
    public async Task GitWorkspaceManager_PublishAsync_CommitsMergesAndPushes()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-worker-tests", Guid.NewGuid().ToString("N"));
        var remote = Path.Combine(root, "remote.git");
        var source = Path.Combine(root, "source");
        var workspaces = Path.Combine(root, "workspaces");
        Directory.CreateDirectory(root);
        await RunGitForTestAsync("init --bare " + Quote(remote), root);
        await RunGitForTestAsync("init -b main " + Quote(source), root);
        await ConfigureGitUserAsync(source);
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "first");
        await RunGitForTestAsync("add README.md", source);
        await RunGitForTestAsync("commit -m first", source);
        await RunGitForTestAsync("remote add origin " + Quote(remote), source);
        await RunGitForTestAsync("push -u origin main", source);

        var manager = new GitWorkspaceManager();
        var prepared = await manager.PrepareAsync(workspaces, "math", remote, "main", null, null, "Project Worker", "project-worker@example.com", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(prepared.WorkspacePath, "README.md"), "worker");

        var published = await manager.PublishAsync(
            prepared.WorkspacePath,
            remote,
            null,
            null,
            "Project Worker",
            "project-worker@example.com",
            "worker update\nRequirement: req-001 - Checkout search\nRequirement summary: Search orders from checkout.",
            (_, _) => Task.FromResult(new GitConflictResolutionResult(false, "unexpected")),
            CancellationToken.None);
        await RunGitForTestAsync("pull --ff-only", source);
        var commitMessage = await ReadGitForTestAsync("log -1 --pretty=%B", source);

        Assert.True(published.Succeeded);
        Assert.True(published.HasChanges);
        Assert.True(published.Pushed);
        Assert.False(published.ConflictResolved);
        Assert.Equal("worker", await File.ReadAllTextAsync(Path.Combine(source, "README.md")));
        Assert.Contains("\"path\":\"README.md\"", published.ChangedFilesJson);
        Assert.Contains("\"status\":\"modified\"", published.ChangedFilesJson);
        Assert.Contains("Requirement: req-001 - Checkout search", commitMessage);
        Assert.Contains("Requirement summary: Search orders from checkout.", commitMessage);
        Assert.Equal("Project Worker", await ReadGitForTestAsync("config user.name", prepared.WorkspacePath));
        Assert.Equal("project-worker@example.com", await ReadGitForTestAsync("config user.email", prepared.WorkspacePath));
    }

    [Fact]
    public void GitWorkspaceManager_BuildChangedFilesJson_ParsesPorcelainStatus()
    {
        var json = GitWorkspaceManager.BuildChangedFilesJson("""
             M README.md
            ?? src/NewFile.cs
            R  old.txt -> new.txt
            D  removed.txt
            """);

        Assert.Contains("\"path\":\"README.md\"", json);
        Assert.Contains("\"status\":\"modified\"", json);
        Assert.Contains("\"path\":\"src/NewFile.cs\"", json);
        Assert.Contains("\"status\":\"added\"", json);
        Assert.Contains("\"path\":\"new.txt\"", json);
        Assert.Contains("\"oldPath\":\"old.txt\"", json);
        Assert.Contains("\"status\":\"deleted\"", json);
    }

    [Fact]
    public void GitWorkspaceManager_BuildChangedFilesJsonFromNameStatus_ParsesCommittedDiff()
    {
        var json = GitWorkspaceManager.BuildChangedFilesJsonFromNameStatus("""
            M	src/Changed.cs
            A	src/NewFile.cs
            R100	old.txt	new.txt
            D	removed.txt
            """);

        Assert.Contains("\"path\":\"src/Changed.cs\"", json);
        Assert.Contains("\"status\":\"modified\"", json);
        Assert.Contains("\"path\":\"src/NewFile.cs\"", json);
        Assert.Contains("\"status\":\"added\"", json);
        Assert.Contains("\"path\":\"new.txt\"", json);
        Assert.Contains("\"oldPath\":\"old.txt\"", json);
        Assert.Contains("\"status\":\"deleted\"", json);
    }

    [Fact]
    public async Task GitWorkspaceManager_PublishAsync_ReportsFilesFromFullPublishedCommitRange()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-worker-tests", Guid.NewGuid().ToString("N"));
        var remote = Path.Combine(root, "remote.git");
        var source = Path.Combine(root, "source");
        var workspaces = Path.Combine(root, "workspaces");
        Directory.CreateDirectory(root);
        await RunGitForTestAsync("init --bare " + Quote(remote), root);
        await RunGitForTestAsync("init -b main " + Quote(source), root);
        await ConfigureGitUserAsync(source);
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "first");
        await RunGitForTestAsync("add README.md", source);
        await RunGitForTestAsync("commit -m first", source);
        await RunGitForTestAsync("remote add origin " + Quote(remote), source);
        await RunGitForTestAsync("push -u origin main", source);

        var manager = new GitWorkspaceManager();
        var prepared = await manager.PrepareAsync(workspaces, "math", remote, "main", null, null, "Project Worker", "project-worker@example.com", CancellationToken.None);
        var baselineCommit = prepared.Commit;
        await File.WriteAllTextAsync(Path.Combine(prepared.WorkspacePath, "already-committed.txt"), "codex committed");
        await RunGitForTestAsync("add already-committed.txt", prepared.WorkspacePath);
        await RunGitForTestAsync("commit -m \"codex self commit\"", prepared.WorkspacePath);
        await File.WriteAllTextAsync(Path.Combine(prepared.WorkspacePath, "pending.txt"), "worker pending");

        var published = await manager.PublishAsync(
            prepared.WorkspacePath,
            remote,
            null,
            null,
            "Project Worker",
            "project-worker@example.com",
            "worker update",
            (_, _) => Task.FromResult(new GitConflictResolutionResult(false, "unexpected")),
            CancellationToken.None,
            baselineCommit);

        Assert.True(published.Succeeded);
        Assert.True(published.Pushed);
        Assert.Contains("\"path\":\"already-committed.txt\"", published.ChangedFilesJson);
        Assert.Contains("\"path\":\"pending.txt\"", published.ChangedFilesJson);
    }

    [Fact]
    public async Task GitWorkspaceManager_PublishAsync_UsesResolverWhenRemoteMergeConflicts()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-worker-tests", Guid.NewGuid().ToString("N"));
        var remote = Path.Combine(root, "remote.git");
        var source = Path.Combine(root, "source");
        var workspaces = Path.Combine(root, "workspaces");
        Directory.CreateDirectory(root);
        await RunGitForTestAsync("init --bare " + Quote(remote), root);
        await RunGitForTestAsync("init -b main " + Quote(source), root);
        await ConfigureGitUserAsync(source);
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "first");
        await RunGitForTestAsync("add README.md", source);
        await RunGitForTestAsync("commit -m first", source);
        await RunGitForTestAsync("remote add origin " + Quote(remote), source);
        await RunGitForTestAsync("push -u origin main", source);

        var manager = new GitWorkspaceManager();
        var prepared = await manager.PrepareAsync(workspaces, "math", remote, "main", null, null, "Project Worker", "project-worker@example.com", CancellationToken.None);
        await ConfigureGitUserAsync(prepared.WorkspacePath);
        await File.WriteAllTextAsync(Path.Combine(prepared.WorkspacePath, "README.md"), "local worker");
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "remote update");
        await RunGitForTestAsync("add README.md", source);
        await RunGitForTestAsync("commit -m remote", source);
        await RunGitForTestAsync("push", source);

        var resolverCalled = false;
        var published = await manager.PublishAsync(
            prepared.WorkspacePath,
            remote,
            null,
            null,
            "Project Worker",
            "project-worker@example.com",
            "worker update",
            async (request, _) =>
            {
                resolverCalled = true;
                Assert.Contains("README.md", request.ConflictFiles);
                await File.WriteAllTextAsync(
                    Path.Combine(request.WorkspacePath, "README.md"),
                    "local worker\n# Remote side kept for review: remote update");
                return new GitConflictResolutionResult(true, null);
            },
            CancellationToken.None);
        await RunGitForTestAsync("pull", source);

        Assert.True(resolverCalled);
        Assert.True(published.Succeeded);
        Assert.True(published.Pushed);
        Assert.True(published.ConflictResolved);
        var content = await File.ReadAllTextAsync(Path.Combine(source, "README.md"));
        Assert.Contains("local worker", content);
        Assert.Contains("Remote side kept for review", content);
    }

    [Fact]
    public async Task GitWorkspaceManager_PrepareAsync_WithoutRepositoryCreatesUnavailableWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-worker-tests", Guid.NewGuid().ToString("N"));
        var manager = new GitWorkspaceManager();

        var result = await manager.PrepareAsync(root, "math", null, null, null, null, null, null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(result.RepositoryAvailable);
        Assert.True(Directory.Exists(result.WorkspacePath));
    }

    [Fact]
    public void GitWorkspaceManager_BuildAuthenticatedUrl_UsesEscapedGitCredential()
    {
        var url = GitWorkspaceManager.BuildAuthenticatedUrl(
            "https://example.com/org/repo.git",
            "codex user",
            "token:123");

        Assert.Equal("https://codex%20user:token%3A123@example.com/org/repo.git", url);
        Assert.Equal(
            "fatal: <redacted>",
            GitWorkspaceManager.SanitizeGitMessage("fatal: token:123", ["token:123"]));
    }

    [Fact]
    public void WorkerDiagnostics_TrimAndRedact_RemovesGitCredentials()
    {
        var redacted = WorkerDiagnostics.TrimAndRedact(
            "git clone https://codex%20user:token%3A123@example.com/org/repo.git",
            ["token%3A123"]);

        Assert.DoesNotContain("token%3A123", redacted);
        Assert.Contains("***REDACTED***:***REDACTED***@example.com", redacted);
    }

    [Fact]
    public void WorkerRuntimeConfigApplier_BuildsCodexConfigWithoutInlineToken()
    {
        var previousApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var options = Options.Create(new WorkerOptions
        {
            CodexModel = "gpt-5.4",
            CodexProvider = "openai",
            OpenAiApiKey = "platform-api-key",
            OpenAiBaseUrl = "https://api.openai.com/v1",
            SandboxMode = "workspace-write"
        });
        var applier = new WorkerRuntimeConfigApplier(options);

        try
        {
            var configToml = applier.BuildCodexConfig("secret-token");

            Assert.Contains("model = \"gpt-5.4\"", configToml);
            Assert.Contains("model_provider = \"agentsprint\"", configToml);
            Assert.Contains("base_url = \"https://api.openai.com/v1\"", configToml);
            Assert.Contains("env_key = \"OPENAI_API_KEY\"", configToml);
            Assert.Contains("wire_api = \"responses\"", configToml);
            Assert.Equal("platform-api-key", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            Assert.DoesNotContain("platform-api-key", configToml);
            Assert.DoesNotContain("bearer_token_env_var = \"AGENTSPRINT_AGENT_TOKEN\"", configToml);
            Assert.DoesNotContain("[mcp_servers.agentsprint]", configToml);
            Assert.DoesNotContain("secret-token", configToml);

            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "container-api-key");
            options.Value.OpenAiApiKey = null;
            applier.BuildCodexConfig("secret-token");
            Assert.Equal("container-api-key", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", previousApiKey);
        }
    }

    [Fact]
    public async Task AgentSprintApiClient_GetRuntimeConfig_UsesTokenScopedRoute()
    {
        var handler = new CapturingHandler(
            """{"code":0,"data":{"workerId":"worker-id","workerCode":"worker-code","workerName":"Worker","projectId":null,"projectCode":null,"workspaceRoot":"/workspaces","runsRoot":"/runs","codexHome":"/codex-home","pollIntervalSeconds":15,"idleMaxIntervalSeconds":180,"maxRunMinutes":15,"sandboxMode":"workspace-write","runSmokeOnStartup":false,"smokePrompt":"hello","codexProvider":"openai","codexModel":"gpt-5.4","openAiBaseUrl":null,"agentToken":"agent-token","configVersion":2},"message":"ok"}""");
        var client = new AgentSprintApiClient(
            new HttpClient(handler),
            Options.Create(new AgentSprintOptions
            {
                ApiBaseUrl = "http://agentsprint.test/",
                AgentToken = "deploy-token"
            }));

        var config = await client.GetRuntimeConfigAsync(CancellationToken.None);

        Assert.Equal("worker-id", config.WorkerId);
        Assert.Equal("/worker-runtime/config", handler.LastRequestUri?.AbsolutePath);
        Assert.Equal("Bearer", handler.LastAuthorizationScheme);
        Assert.Equal("deploy-token", handler.LastAuthorizationParameter);
    }

    private static async Task RunGitForTestAsync(string arguments, string workingDirectory)
    {
        var result = await ProcessCommandRunner.RunAsync(
            "git",
            arguments,
            workingDirectory,
            TimeSpan.FromSeconds(30),
            CancellationToken.None);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Stderr + result.Stdout + result.Error);
        }
    }

    private static async Task<string> ReadGitForTestAsync(string arguments, string workingDirectory)
    {
        var result = await ProcessCommandRunner.RunAsync(
            "git",
            arguments,
            workingDirectory,
            TimeSpan.FromSeconds(30),
            CancellationToken.None);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Stderr + result.Stdout + result.Error);
        }

        return result.Stdout.Trim();
    }

    private static async Task ConfigureGitUserAsync(string workingDirectory)
    {
        await RunGitForTestAsync("config user.email worker@example.com", workingDirectory);
        await RunGitForTestAsync("config user.name Worker", workingDirectory);
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "deploy", "docker", "docker-compose.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the AgentSprint repository root.");
    }
}

internal sealed class CapturingHandler(string responseBody) : HttpMessageHandler
{
    public Uri? LastRequestUri { get; private set; }

    public string? LastAuthorizationScheme { get; private set; }

    public string? LastAuthorizationParameter { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
        LastAuthorizationParameter = request.Headers.Authorization?.Parameter;
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody)
        });
    }
}
