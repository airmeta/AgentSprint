using AgentSprint.Worker.Services;

namespace AgentSprint.Tests;

public sealed class CodexProcessRunnerTests
{
    [Theory]
    [InlineData("Request failed with status 403 Forbidden", "authentication")]
    [InlineData("OpenAI API error 502 Bad Gateway", "upstream")]
    [InlineData("fetch failed: ENOTFOUND api.openai.com", "network")]
    [InlineData("rate limit exceeded: 429", "rate limit")]
    public void TryClassifyFatalOutputLine_DetectsFastFailSignals(string line, string expectedReason)
    {
        var detected = CodexProcessRunner.TryClassifyFatalOutputLine(line, out var reason);

        Assert.True(detected);
        Assert.Contains(expectedReason, reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Started codex exec for run smoke-20260614113807.")]
    [InlineData("thinking")]
    public void TryClassifyFatalOutputLine_IgnoresNormalProgress(string line)
    {
        var detected = CodexProcessRunner.TryClassifyFatalOutputLine(line, out var reason);

        Assert.False(detected);
        Assert.Equal(string.Empty, reason);
    }
}
