using AgentSprint.Model.Modules.Agile;

namespace AgentSprint.Service.Services.AgileServices;

public interface IRequirementDecompositionService
{
    /// <summary>
    /// zh-cn: 根据需求内容和补充指令生成可持久化的开发任务明细；默认实现使用本地规则，后续可替换为 AI/MCP 拆解服务。
    /// en-us: Generates persistable development-task details from requirement content and optional instructions; the default implementation uses local rules and can later be replaced by an AI/MCP decomposition service.
    /// </summary>
    /// <param name="requirement">
    /// zh-cn: 需要拆解的需求实体。
    /// en-us: Requirement entity to decompose.
    /// </param>
    /// <param name="instruction">
    /// zh-cn: 产品或架构补充拆解指令。
    /// en-us: Product or architecture supplemental decomposition instruction.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 发起拆解的用户标识，将写入任务创建人。
    /// en-us: User identifier initiating decomposition, written as task creator.
    /// </param>
    /// <returns>
    /// zh-cn: 待创建的开发任务集合。
    /// en-us: Development tasks to create.
    /// </returns>
    Task<IReadOnlyList<SprintDevelopmentTaskEntity>> DecomposeAsync(
        SprintRequirementEntity requirement,
        string? instruction,
        string userId);
}
