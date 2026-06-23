using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Repository.DbContexts;
using AgentSprint.Service.Services.AgileServices;

using Air.Cloud.Core;
using Air.Cloud.Modules.Akka.Actors;
using Air.Cloud.Modules.Akka.Attributes;

using Akka.Actor;

using Microsoft.Extensions.DependencyInjection;

namespace AgentSprint.Entry.Actors;

[AkkaActor(WorkerPlatformActorNames.WorkerCommandLogReceiver, Domain = WorkerPlatformActorNames.Domain, Role = WorkerPlatformActorNames.Role)]
public sealed class WorkerCommandLogReceiverActor : AirActorBase
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// <para>zh-cn: 创建平台端 Worker 命令日志接收 Actor。Actor 由 Air.Cloud.Modules.Akka 自动注册，并按 mailbox 顺序把日志增量写入 Redis 缓冲；完成标记会触发数据库归档。</para>
    /// <para>en-us: Creates the platform-side Worker command-log receiver actor. Air.Cloud.Modules.Akka auto-registers it, and its mailbox serially appends chunks into the Redis buffer; completion markers trigger database archival.</para>
    /// </summary>
    public WorkerCommandLogReceiverActor()
    {
        _serviceProvider = PlatformActorDependencyAccessor.GetRequiredService<IServiceProvider>();
        ReceiveAsync<WorkerCommandLogChunkMessage>(AppendAsync);
    }

    private async Task AppendAsync(WorkerCommandLogChunkMessage message)
    {
        var sender = Sender;
        try
        {
            AppRealization.TraceLog.Write(
                AppRealization.JSON.Serialize(new
                {
                    level = "Information",
                    message = "Received worker command log from Akka.",
                    message.WorkerId,
                    message.CommandId,
                    message.RunId,
                    message.Sequence,
                    message.Completed
                }),
                new Dictionary<string, string>()
                {
                    { "workerId", message.WorkerId },
                    { "commandId", message.CommandId },
                    { "runId", message.RunId ?? "<null>" }
                });

            await using var scope = _serviceProvider.CreateAsyncScope();
            var runtimeService = scope.ServiceProvider.GetRequiredService<IDigitalWorkerRuntimeService>();
            await runtimeService.AppendCommandLogAsync(
                message.WorkerId,
                new AppendWorkerCommandLogRequest(
                    message.CommandId,
                    message.Chunk,
                    message.SessionId,
                    message.RunId,
                    message.InstanceId,
                    message.Sequence,
                    message.Completed,
                    message.StartedAt,
                    message.CompletedAt));
            var dbContext = scope.ServiceProvider.GetRequiredService<DefaultDbContext>();
            await dbContext.SaveChangesAsync();
            sender.Tell(
                new WorkerCommandLogAckMessage(
                    message.CommandId,
                    message.RunId,
                    message.Sequence,
                    message.Completed),
                Self);
        }
        catch (Exception ex)
        {
            AppRealization.TraceLog.Write(
                AppRealization.JSON.Serialize(new
                {
                    level = "Warning",
                    message = "Failed to append worker command log from Akka.",
                    message.WorkerId,
                    message.CommandId,
                    message.RunId,
                    message.Sequence,
                    message.Completed,
                    exception = ex.ToString()
                }),
                new Dictionary<string, string>()
                {
                    { "workerId", message.WorkerId },
                    { "commandId", message.CommandId },
                    { "runId", message.RunId ?? "<null>" }
                });
        }
    }
}
