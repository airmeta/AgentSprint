using AgentSprint.Entry;

using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Tests;

public sealed class AgentSprintUnifyResultProviderTests
{
    [Fact]
    public void OnSucceeded_WrapsPlainDataWithFrontendEnvelope()
    {
        var provider = new AgentSprintUnifyResultProvider();

        var result = Assert.IsType<JsonResult>(provider.OnSucceeded(null!, new { Name = "demo" }));
        var response = Assert.IsType<ApiResponse<object>>(result.Value);

        Assert.Equal(0, response.Code);
        Assert.Equal("ok", response.Message);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public void OnSucceeded_DoesNotDoubleWrapExistingApiResponse()
    {
        var provider = new AgentSprintUnifyResultProvider();
        var existing = ApiResponse<string>.Ok("ready");

        var result = Assert.IsType<JsonResult>(provider.OnSucceeded(null!, existing));

        Assert.Same(existing, result.Value);
    }
}
