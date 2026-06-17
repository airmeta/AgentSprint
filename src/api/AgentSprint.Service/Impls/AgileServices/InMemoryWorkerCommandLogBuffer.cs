using System.Collections.Concurrent;
using System.Text;

using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Service.Services.AgileServices;

namespace AgentSprint.Service.Impls.AgileServices;

public sealed class InMemoryWorkerCommandLogBuffer : IWorkerCommandLogBuffer
{
    private readonly ConcurrentDictionary<string, CommandLogBufferState> _buffers = new(StringComparer.Ordinal);

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
        var state = _buffers.GetOrAdd(commandId, _ => new CommandLogBufferState());
        lock (state.SyncRoot)
        {
            state.WorkerId = workerId;
            state.CommandId = commandId;
            state.SessionId = sessionId ?? state.SessionId;
            state.RunId = runId ?? state.RunId;
            state.InstanceId = instanceId;
            if (!string.IsNullOrEmpty(chunk) && sequence >= state.LastSequence)
            {
                state.Builder.Append(chunk);
                state.LastSequence = sequence;
            }

            state.Completed = completed || state.Completed;
            state.UpdatedAt = DateTime.UtcNow;
            return state.ToSnapshot();
        }
    }

    /// <inheritdoc />
    public WorkerCommandLogSnapshotResult? Get(string commandId)
    {
        if (!_buffers.TryGetValue(commandId, out var state))
        {
            return null;
        }

        lock (state.SyncRoot)
        {
            return state.ToSnapshot();
        }
    }

    /// <inheritdoc />
    public void Remove(string commandId)
    {
        _buffers.TryRemove(commandId, out _);
    }

    private sealed class CommandLogBufferState
    {
        public object SyncRoot { get; } = new();

        public string WorkerId { get; set; } = string.Empty;

        public string CommandId { get; set; } = string.Empty;

        public string? SessionId { get; set; }

        public string? RunId { get; set; }

        public string InstanceId { get; set; } = string.Empty;

        public StringBuilder Builder { get; } = new();

        public long LastSequence { get; set; }

        public bool Completed { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public WorkerCommandLogSnapshotResult ToSnapshot()
        {
            return new WorkerCommandLogSnapshotResult(
                CommandId,
                RunId,
                SessionId,
                InstanceId,
                Builder.ToString(),
                LastSequence,
                Completed,
                UpdatedAt);
        }
    }
}
