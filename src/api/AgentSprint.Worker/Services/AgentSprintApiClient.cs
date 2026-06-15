using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AgentSprint.Worker.Models;
using AgentSprint.Worker.Options;

using Air.Cloud.Core;

using Microsoft.Extensions.Options;

namespace AgentSprint.Worker.Services;

public sealed class AgentSprintApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AgentSprintOptions _options;

    /// <summary>
    /// <para>zh-cn:创建 AgentSprint 主平台 HTTP 客户端。客户端统一应用平台基础地址和数字员工 Agent Token；日志只记录连接目标，不输出 Token 或其他 Secret。</para>
    /// <para>en-us:Creates the AgentSprint platform HTTP client. The client centralizes the platform base URL and digital-worker Agent Token; logs record only the connection target and never print tokens or other secrets.</para>
    /// </summary>
    /// <param name="httpClient">
    /// <para>zh-cn:由 IHttpClientFactory 创建的 HTTP 客户端。</para>
    /// <para>en-us:HTTP client created by IHttpClientFactory.</para>
    /// </param>
    /// <param name="options">
    /// <para>zh-cn:AgentSprint 平台连接配置。</para>
    /// <para>en-us:AgentSprint platform connection options.</para>
    /// </param>
    public AgentSprintApiClient(
        HttpClient httpClient,
        IOptions<AgentSprintOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress = new Uri(_options.ApiBaseUrl, UriKind.Absolute);
        if (!string.IsNullOrWhiteSpace(_options.AgentToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.AgentToken);
        }
    }

    public void UseAgentToken(string? agentToken)
    {
        if (string.IsNullOrWhiteSpace(agentToken))
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
    }

    /// <summary>
    /// <para>zh-cn:执行主平台连接探针。当前 MVP 只验证基础地址是否可解析，并为后续注册、心跳、命令 ACK 和事件上报保留统一客户端入口。</para>
    /// <para>en-us:Runs a platform connectivity probe. The current MVP only verifies that the base address can be resolved and keeps a unified client entry point for later registration, heartbeat, command ACK, and event reporting.</para>
    /// </summary>
    /// <param name="cancellationToken">
    /// <para>zh-cn:取消令牌。</para>
    /// <para>en-us:Cancellation token.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:连接探针任务。</para>
    /// <para>en-us:Connectivity probe task.</para>
    /// </returns>
    public Task ProbeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            AppRealization.TraceLog.Write(
                AppRealization.JSON.Serialize(new
                {
                    level = "Information",
                    message = "AgentSprint API client configured.",
                    apiBaseUrl = _httpClient.BaseAddress?.ToString() ?? "<null>"
                }),
                new Dictionary<string, string>()
                {
                    { "apiBaseUrl", _httpClient.BaseAddress?.ToString() ?? "<null>" }
                });
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    public Task<WorkerRuntimeConfigResult> GetRuntimeConfigAsync(
        string workerId,
        CancellationToken cancellationToken)
    {
        return GetAsync<WorkerRuntimeConfigResult>($"worker-runtime/config/{Uri.EscapeDataString(workerId)}", cancellationToken);
    }

    /// <summary>
    /// <para>zh-cn: 使用部署注入的 Agent Token 获取平台托管 Worker 配置。该方法调用无 WorkerId 路由，WorkerId 由平台根据令牌绑定的数字员工主档返回，支持受控端独立部署时只配置 API 地址和 Agent Token。</para>
    /// <para>en-us: Gets platform-managed Worker configuration with the Agent Token injected by deployment. The method calls the route without a WorkerId, so the platform returns the WorkerId from the token-bound digital-worker profile and supports standalone controlled endpoints configured only with API URL and Agent Token.</para>
    /// </summary>
    /// <param name="cancellationToken">
    /// <para>zh-cn:取消令牌。</para>
    /// <para>en-us:Cancellation token.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:平台返回的 Worker 运行配置。</para>
    /// <para>en-us:Worker runtime configuration returned by the platform.</para>
    /// </returns>
    public Task<WorkerRuntimeConfigResult> GetRuntimeConfigAsync(CancellationToken cancellationToken)
    {
        return GetAsync<WorkerRuntimeConfigResult>("worker-runtime/config", cancellationToken);
    }

    public Task<WorkerSessionResult> RegisterSessionAsync(
        RegisterWorkerSessionRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<RegisterWorkerSessionRequest, WorkerSessionResult>(
            "worker-runtime/register-session",
            request,
            cancellationToken);
    }

    public Task<WorkerHeartbeatResult> HeartbeatAsync(
        WorkerHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<WorkerHeartbeatRequest, WorkerHeartbeatResult>(
            "worker-runtime/heartbeat",
            request,
            cancellationToken);
    }

    public Task<WorkerCommandResult> AckCommandAsync(
        string commandId,
        AckWorkerCommandRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<AckWorkerCommandRequest, WorkerCommandResult>(
            $"worker-runtime/commands/{commandId}/ack",
            request,
            cancellationToken);
    }

    public Task<WorkerCommandResult> StartCommandAsync(
        string commandId,
        AckWorkerCommandRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<AckWorkerCommandRequest, WorkerCommandResult>(
            $"worker-runtime/commands/{commandId}/start",
            request,
            cancellationToken);
    }

    public Task<WorkerRunResult> StartRunAsync(
        StartWorkerRunRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<StartWorkerRunRequest, WorkerRunResult>(
            "worker-runtime/runs/start",
            request,
            cancellationToken);
    }

    public Task<WorkerRunResult> FinishRunAsync(
        string runId,
        FinishWorkerRunRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<FinishWorkerRunRequest, WorkerRunResult>(
            $"worker-runtime/runs/{runId}/finish",
            request,
            cancellationToken);
    }

    public Task<WorkerPromptResult> GetWorkPromptAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken)
    {
        return GetAsync<WorkerPromptResult>(
            $"worker-runtime/work/{Uri.EscapeDataString(targetType)}/{Uri.EscapeDataString(targetId)}/prompt",
            cancellationToken);
    }

    public Task<WorkerWorkCompletionResult> CompleteWorkAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken)
    {
        return PostAsync<object, WorkerWorkCompletionResult>(
            $"worker-runtime/work/{Uri.EscapeDataString(targetType)}/{Uri.EscapeDataString(targetId)}/complete",
            new { },
            cancellationToken);
    }

    public Task<WorkerEventResult> ReportEventAsync(
        ReportWorkerEventRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<ReportWorkerEventRequest, WorkerEventResult>(
            "worker-runtime/events",
            request,
            cancellationToken);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, request, JsonOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AgentSprint API request failed. Path={path}, Status={(int)response.StatusCode}, Body={body}");
        }

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(body, JsonOptions);
        if (apiResponse is null)
        {
            throw new InvalidOperationException($"AgentSprint API returned an invalid response. Path={path}");
        }

        if (apiResponse.Code != 0 || apiResponse.Data is null)
        {
            throw new InvalidOperationException(
                $"AgentSprint API returned an error. Path={path}, Code={apiResponse.Code}, Message={apiResponse.Message}");
        }

        return apiResponse.Data;
    }

    private async Task<TResponse> GetAsync<TResponse>(
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AgentSprint API request failed. Path={path}, Status={(int)response.StatusCode}, Body={body}");
        }

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(body, JsonOptions);
        if (apiResponse is null)
        {
            throw new InvalidOperationException($"AgentSprint API returned an invalid response. Path={path}");
        }

        if (apiResponse.Code != 0 || apiResponse.Data is null)
        {
            throw new InvalidOperationException(
                $"AgentSprint API returned an error. Path={path}, Code={apiResponse.Code}, Message={apiResponse.Message}");
        }

        return apiResponse.Data;
    }
}
