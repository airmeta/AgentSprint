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
        string userId,
        int? taskCount = null)
    {
        var description = NormalizeOptional(requirement.Description) ?? requirement.Title;
        var instructionLine = NormalizeOptional(instruction);
        var baseDescription = instructionLine is null ? description : $"{description}\n\n拆解补充：{instructionLine}";
        var normalizedTaskCount = NormalizeTaskCount(taskCount);

        var blueprints = normalizedTaskCount == 1 ? SingleTaskBlueprints : MultiTaskBlueprints;

        IReadOnlyList<SprintDevelopmentTaskEntity> tasks = Enumerable
            .Range(0, normalizedTaskCount)
            .Select(index =>
            {
                var blueprint = blueprints[Math.Min(index, blueprints.Length - 1)];
                var titlePrefix = index < blueprints.Length
                    ? blueprint.TitlePrefix
                    : $"扩展交付任务 {index + 1}";

                return new SprintDevelopmentTaskEntity
                {
                    ProjectId = requirement.ProjectId,
                    RequirementId = requirement.Id,
                    Title = $"{titlePrefix} - {requirement.Title}",
                    Description = $"{blueprint.DescriptionPrefix}\n\n{baseDescription}",
                    Priority = Math.Max(1, requirement.Priority + blueprint.PriorityOffset + Math.Max(0, index - blueprints.Length + 1)),
                    CreatedBy = userId
                };
            })
            .ToList();

        return Task.FromResult(tasks);
    }

    private static int NormalizeTaskCount(int? taskCount)
    {
        return taskCount is null or < 1 ? 1 : taskCount.Value;
    }

    private static readonly (string TitlePrefix, string DescriptionPrefix, int PriorityOffset)[] SingleTaskBlueprints =
    [
        ("完成需求交付", "按需求内容完成实现、验证和必要的交付说明。", 0)
    ];

    private static readonly (string TitlePrefix, string DescriptionPrefix, int PriorityOffset)[] MultiTaskBlueprints =
    [
        ("梳理实现方案", "阅读需求内容并确认接口、数据模型和页面影响范围。", 0),
        ("实现业务闭环", "完成后端服务、管理端页面和必要的数据联动。", 1),
        ("补充验证用例", "补充单元测试、接口验证和管理端回归检查。", 2)
    ];

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
