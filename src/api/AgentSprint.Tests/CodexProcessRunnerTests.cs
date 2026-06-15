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
    [InlineData("- Git commit: 85f21ff29849b29f340942d0e648401926bb890d")]
    [InlineData("- Requirement ID: e3babbea1899412ab15b95e74ac4684d")]
    public void TryClassifyFatalOutputLine_IgnoresNormalProgress(string line)
    {
        var detected = CodexProcessRunner.TryClassifyFatalOutputLine(line, out var reason);

        Assert.False(detected);
        Assert.Equal(string.Empty, reason);
    }
}
