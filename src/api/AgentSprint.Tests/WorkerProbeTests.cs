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
        var cloned = await manager.PrepareAsync(workspaces, "math", remote, "main", null, null, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "second");
        await RunGitForTestAsync("add README.md", source);
        await RunGitForTestAsync("commit -m second", source);
        await RunGitForTestAsync("push", source);

        var pulled = await manager.PrepareAsync(workspaces, "math", remote, "main", null, null, CancellationToken.None);

        Assert.True(cloned.Succeeded);
        Assert.True(pulled.Succeeded);
        Assert.True(pulled.RepositoryAvailable);
        Assert.Equal("main", pulled.Branch);
        Assert.False(pulled.Dirty);
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(pulled.WorkspacePath, "README.md")));
    }

    [Fact]
    public async Task GitWorkspaceManager_PrepareAsync_WithoutRepositoryCreatesUnavailableWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-worker-tests", Guid.NewGuid().ToString("N"));
        var manager = new GitWorkspaceManager();

        var result = await manager.PrepareAsync(root, "math", null, null, null, null, CancellationToken.None);

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
    public void WorkerRuntimeConfigApplier_BuildsCodexConfigWithoutInlineToken()
    {
        var options = Options.Create(new WorkerOptions
        {
            CodexModel = "gpt-5.4",
            CodexProvider = "openai",
            OpenAiBaseUrl = "https://api.openai.com/v1",
            SandboxMode = "workspace-write"
        });
        var applier = new WorkerRuntimeConfigApplier(options);

        var configToml = applier.BuildCodexConfig("secret-token");

        Assert.Contains("model = \"gpt-5.4\"", configToml);
        Assert.Contains("base_url = \"https://api.openai.com/v1\"", configToml);
        Assert.Contains("bearer_token_env_var = \"AGENTSPRINT_AGENT_TOKEN\"", configToml);
        Assert.DoesNotContain("[mcp_servers.agentsprint]", configToml);
        Assert.DoesNotContain("secret-token", configToml);
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

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
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
