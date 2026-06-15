using Air.Cloud.Modules.Akka.Actors;
using Air.Cloud.Modules.Akka.Attributes;

using AgentSprint.Worker.Models;
using AgentSprint.Worker.Services;

using Air.Cloud.Core;

namespace AgentSprint.Worker.Actors;

[AkkaActor(WorkerActorNames.EventReporter, Domain = WorkerActorNames.Domain, Role = WorkerActorNames.Role)]
public sealed class WorkerEventReporterActor : AirActorBase
{
    private readonly AgentSprintApiClient _apiClient;

    /// <summary>
    /// <para>zh-cn:创建数字员工事件上报 Actor。该 Actor 由 Air.Cloud.Modules.Akka 自动注册，按 mailbox 顺序串行接收事件消息并调用平台事件接口；上报失败只记录本地告警，不阻断 Worker 的主任务链路。</para>
    /// <para>en-us:Creates the digital-worker event reporting actor. Air.Cloud.Modules.Akka auto-registers it, it serially receives event messages through its mailbox and calls the platform event API; reporting failures are logged locally and do not block the Worker's main task flow.</para>
    /// </summary>
    public WorkerEventReporterActor()
    {
        _apiClient = WorkerActorDependencyAccessor.GetRequiredService<AgentSprintApiClient>();

        ReceiveAsync<WorkerEventReportMessage>(ReportAsync);
    }

    private async Task ReportAsync(WorkerEventReportMessage message)
    {
        try
        {
            await _apiClient.ReportEventAsync(
                new ReportWorkerEventRequest(
                    message.WorkerId,
                    message.EventType,
                    message.Message,
                    message.SessionId,
                    message.RunId,
                    message.Level,
                    message.PayloadJson),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            try
            {
                AppRealization.TraceLog.Write(
                    AppRealization.JSON.Serialize(new
                    {
                        level = "Warning",
                        message = "Failed to report worker event.",
                        eventType = message.EventType,
                        workerId = message.WorkerId,
                        sessionId = message.SessionId,
                        runId = message.RunId,
                        exception = ex.ToString()
                    }),
                    new Dictionary<string, string>()
                    {
                        { "eventType", message.EventType },
                        { "workerId", message.WorkerId },
                        { "sessionId", message.SessionId ?? "<null>" },
                        { "runId", message.RunId ?? "<null>" }
                    });
            }
            catch
            {
            }
        }
    }
}
