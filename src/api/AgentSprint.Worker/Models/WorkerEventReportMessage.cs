namespace AgentSprint.Worker.Models;

public sealed record WorkerEventReportMessage(
    string WorkerId,
    string EventType,
    string Message,
    string? SessionId,
    string? RunId,
    string? Level,
    string? PayloadJson);
