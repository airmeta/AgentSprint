using AgentSprint.Model.Modules.Security.Dtos;

namespace AgentSprint.Service.Services.SecurityServices;

public interface IAiConversationService
{
    Task<IReadOnlyList<AiConversationResult>> ListConversationsAsync(
        string? keyword = null,
        string? projectId = null,
        string? requirementId = null,
        string? taskId = null,
        string? testPlanId = null,
        string? bugId = null,
        string? status = null);

    Task<AiConversationResult> StartConversationAsync(StartAiConversationRequest request, string createdBy);
}
