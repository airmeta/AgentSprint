using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Service.Impls.AgileServices;
using AgentSprint.Service.Services.AgileServices;

namespace AgentSprint.Tests;

public sealed class GitManagementServiceTests
{
    [Fact]
    public async Task CreateRepositoryAsync_BindsExistingGitAccount()
    {
        var accountDomain = new InMemoryGitAccountDomain();
        var repositoryDomain = new InMemoryGitRepositoryDomain();
        var service = CreateService(accountDomain, repositoryDomain);
        var account = await service.CreateAccountAsync(
            new SaveGitAccountRequest("MAIN", "Main account", "codex", "token"),
            "admin");

        var repository = await service.CreateRepositoryAsync(
            new SaveGitRepositoryRequest(
                "AGENTSPRINT",
                "AgentSprint",
                "https://example.com/agentsprint.git",
                "main",
                account.Id),
            "admin");

        Assert.Equal(account.Id, repository.GitAccountId);
        Assert.Equal("https://example.com/agentsprint.git", repository.RepositoryUrl);
        Assert.Equal("main", repository.DefaultBranch);
    }

    [Fact]
    public async Task DeleteBranchAsync_CreatesBackupBeforeDeletingBranch()
    {
        var operationDomain = new InMemoryGitBranchOperationDomain();
        var runner = new FakeGitCommandRunner();
        var service = CreateService(operationDomain: operationDomain, runner: runner);
        var repository = await service.CreateRepositoryAsync(
            new SaveGitRepositoryRequest(
                "AGENTSPRINT",
                "AgentSprint",
                "https://example.com/agentsprint.git",
                "main",
                LocalPath: Path.GetTempPath()),
            "admin");

        var result = await service.DeleteBranchAsync(
            repository.Id,
            new DeleteGitBranchRequest("feature/a", "backup/feature-a"),
            "admin");

        Assert.Equal(GitBranchOperationStatuses.Success, result.Status);
        Assert.Equal("backup/feature-a", result.BackupBranch);
        Assert.Contains(runner.Commands, command => command.Contains("switch -c backup/feature-a origin/feature/a"));
        Assert.Contains(runner.Commands, command => command.Contains("push -u origin backup/feature-a"));
        Assert.Contains(runner.Commands, command => command.Contains("push origin --delete feature/a"));
        Assert.Single(await operationDomain.ListAsync(entity => entity.OperationType == GitBranchOperationTypes.DeleteBranch));
    }

    [Fact]
    public async Task ReadBranchPushRecordsAsync_StoresCommitSnapshots()
    {
        var operationDomain = new InMemoryGitBranchOperationDomain();
        var runner = new FakeGitCommandRunner
        {
            LogOutput = "abc123\u001f2026-06-14T10:00:00Z\u001fInitial push\u001e"
        };
        var service = CreateService(operationDomain: operationDomain, runner: runner);
        var repository = await service.CreateRepositoryAsync(
            new SaveGitRepositoryRequest(
                "AGENTSPRINT",
                "AgentSprint",
                "https://example.com/agentsprint.git",
                "main",
                LocalPath: Path.GetTempPath()),
            "admin");

        var records = await service.ReadBranchPushRecordsAsync(repository.Id, "main", "admin");

        var record = Assert.Single(records);
        Assert.Equal("abc123", record.CommitHash);
        Assert.Equal("Initial push", record.CommitMessage);
        Assert.Equal("main", record.BranchName);
        Assert.Single(await operationDomain.ListAsync(entity => entity.OperationType == GitBranchOperationTypes.PushRecord));
    }

    private static GitManagementService CreateService(
        InMemoryGitAccountDomain? accountDomain = null,
        InMemoryGitRepositoryDomain? repositoryDomain = null,
        InMemoryGitBranchOperationDomain? operationDomain = null,
        IGitCommandRunner? runner = null)
    {
        return new GitManagementService(
            accountDomain ?? new InMemoryGitAccountDomain(),
            repositoryDomain ?? new InMemoryGitRepositoryDomain(),
            operationDomain ?? new InMemoryGitBranchOperationDomain(),
            runner ?? new FakeGitCommandRunner());
    }
}

internal sealed class FakeGitCommandRunner : IGitCommandRunner
{
    public List<string> Commands { get; } = [];

    public string LogOutput { get; set; } = string.Empty;

    public Task<GitCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        Commands.Add(string.Join(' ', arguments));
        var output = arguments.Count > 0 && arguments[0] == "log" ? LogOutput : string.Empty;
        return Task.FromResult(new GitCommandResult(0, output, string.Empty));
    }
}
