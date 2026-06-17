using System.Text.Json;

using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Service.Services.AgileServices;

using Air.Cloud.Core.Standard.Cache.Redis;

namespace AgentSprint.Entry.Services;

public sealed class RedisWorkerCommandLogBuffer : IWorkerCommandLogBuffer
{
    private static readonly TimeSpan BufferExpiry = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRedisCacheStandard _redisCache;

    /// <summary>
    /// <para>zh-cn: 创建 Redis Worker 命令日志缓冲区，使用 Redis 字符串保存实时快照，使多个 API 实例和 SSE/轮询请求可以读取同一份日志状态。</para>
    /// <para>en-us: Creates the Redis-backed Worker command-log buffer, storing live snapshots as Redis strings so multiple API instances and SSE/polling requests can read the same log state.</para>
    /// </summary>
    public RedisWorkerCommandLogBuffer(IRedisCacheStandard redisCache)
    {
        _redisCache = redisCache;
    }

    /// <inheritdoc />
    public WorkerCommandLogSnapshotResult Append(
        string workerId,
        string commandId,
        string? sessionId,
        string? runId,
        string instanceId,
        string? chunk,
        long sequence,
        bool completed)
    {
        var key = BuildKey(commandId);
        var snapshot = ReadSnapshot(key);
        if (snapshot is null)
        {
            snapshot = new WorkerCommandLogSnapshotResult(
                commandId,
                runId,
                sessionId,
                instanceId,
                string.Empty,
                0,
                false,
                DateTime.UtcNow);
        }

        var logText = snapshot.LogText;
        var lastSequence = snapshot.LastSequence;
        if (!string.IsNullOrEmpty(chunk) && sequence >= snapshot.LastSequence)
        {
            logText += chunk;
            lastSequence = sequence;
        }

        snapshot = new WorkerCommandLogSnapshotResult(
            commandId,
            runId ?? snapshot.RunId,
            sessionId ?? snapshot.SessionId,
            string.IsNullOrWhiteSpace(instanceId) ? snapshot.InstanceId : instanceId,
            logText,
            lastSequence,
            completed || snapshot.Completed,
            DateTime.UtcNow);
        _redisCache.SetCache(key, JsonSerializer.Serialize(snapshot, JsonOptions), BufferExpiry);
        return snapshot;
    }

    /// <inheritdoc />
    public WorkerCommandLogSnapshotResult? Get(string commandId)
    {
        return ReadSnapshot(BuildKey(commandId));
    }

    /// <inheritdoc />
    public void Remove(string commandId)
    {
        _redisCache.RemoveCache(BuildKey(commandId));
    }

    private WorkerCommandLogSnapshotResult? ReadSnapshot(string key)
    {
        var value = _redisCache.GetCache(key);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : JsonSerializer.Deserialize<WorkerCommandLogSnapshotResult>(value, JsonOptions);
    }

    private static string BuildKey(string commandId)
    {
        return "agentsprint:worker-command-log:" + commandId;
    }
}

