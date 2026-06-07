using AgentSprint.Model.Modules.Agile;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.AgileServices;

namespace AgentSprint.Service.Impls.AgileServices;

public sealed class RequirementDecompositionService : AgentSprintServiceBase, IRequirementDecompositionService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SprintDevelopmentTaskEntity>> DecomposeAsync(
        SprintRequirementEntity requirement,
        string? instruction,
        string userId)
    {
        var description = NormalizeOptional(requirement.Description) ?? requirement.Title;
        var instructionLine = NormalizeOptional(instruction);
        var baseDescription = instructionLine is null ? description : $"{description}\n\n拆解补充：{instructionLine}";

        IReadOnlyList<SprintDevelopmentTaskEntity> tasks =
        [
            new SprintDevelopmentTaskEntity
            {
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                Title = $"梳理实现方案 - {requirement.Title}",
                Description = $"阅读需求内容并确认接口、数据模型和页面影响范围。\n\n{baseDescription}",
                Priority = Math.Max(1, requirement.Priority),
                CreatedBy = userId
            },
            new SprintDevelopmentTaskEntity
            {
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                Title = $"实现业务闭环 - {requirement.Title}",
                Description = $"完成后端服务、管理端页面和必要的数据联动。\n\n{baseDescription}",
                Priority = Math.Max(1, requirement.Priority + 1),
                CreatedBy = userId
            },
            new SprintDevelopmentTaskEntity
            {
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                Title = $"补充验证用例 - {requirement.Title}",
                Description = $"补充单元测试、接口验证和管理端回归检查。\n\n{baseDescription}",
                Priority = Math.Max(1, requirement.Priority + 2),
                CreatedBy = userId
            }
        ];

        return Task.FromResult(tasks);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
