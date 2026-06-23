using AgentSprint.Model.Modules.Agile.Dtos;

namespace AgentSprint.Service.Services.AgileServices;

public interface IRequirementDecompositionPreviewService
{
    Task<SprintRequirementDecompositionPreviewResult> PreviewAsync(
        string requirementId,
        string? instruction,
        int? taskCount,
        string? aiPlatformCode,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SprintRequirementDecompositionPreviewResult>> ListAsync(
        string requirementId);

    Task<SprintRequirementDecompositionPreviewResult> SaveDraftAsync(
        string requirementId,
        SaveSprintRequirementDecompositionPreviewRequest request,
        string userId);
}
