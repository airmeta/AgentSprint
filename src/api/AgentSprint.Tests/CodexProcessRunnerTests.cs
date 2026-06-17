using AgentSprint.Worker.Services;
using AgentSprint.Worker.Models;
using AgentSprint.Worker.Options;

using Microsoft.Extensions.Options;

namespace AgentSprint.Tests;

public sealed class CodexProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_IgnoresStdoutContentAndUsesFinalResult()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-codex-runner-tests", Guid.NewGuid().ToString("N"));
        var runsRoot = Path.Combine(root, "runs");
        var workspace = Path.Combine(root, "workspace");
        var fakeCodex = Path.Combine(root, OperatingSystem.IsWindows() ? "fake-codex.cmd" : "fake-codex.sh");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(runsRoot);
        await File.WriteAllTextAsync(
            fakeCodex,
            OperatingSystem.IsWindows()
                ? """
                  @echo off
                  echo unauthorized appears in normal output
                  echo src/admin/apps/web-naive/src/views/_core/authentication/qrcode-login.vue
                  set "final="
                  :loop
                  if "%~1"=="" goto done
                  if "%~1"=="--output-last-message" goto capture
                  shift
                  goto loop
                  :capture
                  shift
                  set "final=%~1"
                  shift
                  goto loop
                  :done
                  echo completed>"%final%"
                  exit /b 0
                  """
                : """
                  #!/bin/sh
                  echo "unauthorized appears in normal output"
                  echo "src/admin/apps/web-naive/src/views/_core/authentication/qrcode-login.vue"
                  final=""
                  while [ "$#" -gt 0 ]; do
                    if [ "$1" = "--output-last-message" ]; then
                      shift
                      final="$1"
                    fi
                    shift
                  done
                  printf "completed\n" > "$final"
                  exit 0
                  """);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(fakeCodex, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        try
        {
            var runner = new CodexProcessRunner(new WorkerRunLogger(Options.Create(new WorkerOptions
            {
                RunsRoot = runsRoot
            })));

            var result = await runner.RunAsync(
                new CodexRunRequest(
                    "run-1",
                    workspace,
                    "hello",
                    "workspace-write",
                    SkipGitRepoCheck: true,
                    TimeSpan.FromSeconds(10),
                    IdleTimeout: TimeSpan.FromSeconds(5),
                    CodexExecutable: fakeCodex),
                CancellationToken.None);

            Assert.Equal("success", result.Status);
            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(result.FinalPath));
            Assert.Contains("unauthorized appears in normal output", await File.ReadAllTextAsync(result.StdoutPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WatchIdleAsync_ReportsWaitingAndTimeoutProgress()
    {
        var events = new List<CodexRunProgressEvent>();
        var startedAt = DateTimeOffset.UtcNow.AddSeconds(-2);

        var error = await CodexProcessRunner.WatchIdleAsync(
            TimeSpan.FromMilliseconds(120),
            () => startedAt,
            () => false,
            progress =>
            {
                events.Add(progress);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Contains("no stdout/stderr", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(events, item => item.EventType == WorkerEventTypes.CodexIdleWaiting && !item.HasOutput);
        Assert.Contains(events, item => item.EventType == WorkerEventTypes.CodexIdleTimeout && item.Level == "error");
    }

    [Fact]
    public async Task BuildLaunchDiagnostics_IncludesConfigFingerprintWithoutSecrets()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentsprint-codex-diagnostics", Guid.NewGuid().ToString("N"));
        var codexHome = Path.Combine(root, "codex-home");
        var workspace = Path.Combine(root, "workspace");
        var originalCodexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var originalOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Directory.CreateDirectory(codexHome);
        Directory.CreateDirectory(workspace);
        await File.WriteAllTextAsync(
            Path.Combine(codexHome, "config.toml"),
            """
            model = "gpt-test"
            model_provider = "agentsprint"

            [model_providers.agentsprint]
            name = "openai"
            base_url = "https://gateway.example.test"
            env_key = "OPENAI_API_KEY"
            """);
        Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "secret-key-value");

        try
        {
            var request = new CodexRunRequest(
                "run-1",
                workspace,
                "hello",
                "workspace-write",
                SkipGitRepoCheck: true,
                TimeSpan.FromMinutes(1));

            var diagnostics = CodexProcessRunner.BuildLaunchDiagnostics(
                request,
                Path.Combine(root, "final.md"),
                TimeSpan.FromSeconds(90));

            Assert.Contains("configExists=True", diagnostics);
            Assert.Contains("configModel=gpt-test", diagnostics);
            Assert.Contains("configProvider=agentsprint", diagnostics);
            Assert.Contains("configBaseUrl=https://gateway.example.test", diagnostics);
            Assert.Contains("hasOPENAI_API_KEY=True", diagnostics);
            Assert.DoesNotContain("secret-key-value", diagnostics);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", originalCodexHome);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalOpenAiApiKey);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
