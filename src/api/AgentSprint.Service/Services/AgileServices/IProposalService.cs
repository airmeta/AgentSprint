using AgentSprint.Model.Modules.Agile.Dtos;

namespace AgentSprint.Service.Services.AgileServices;

public interface IProposalService
{
    /// <summary>
    /// zh-cn: 查询项目提案列表，支持状态、创建人和关键词过滤，并返回分页结果。
    /// en-us: Lists project proposals with status, creator, and keyword filters, returning a paged result.
    /// </summary>
    Task<SprintProposalListResult> ListAsync(
        string projectId,
        string? status,
        string? createdBy,
        string? keyword,
        int pageIndex,
        int pageSize,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 创建提案草稿，并可绑定同项目的项目材料。
    /// en-us: Creates a proposal draft and optionally binds project materials from the same project.
    /// </summary>
    Task<SprintProposalResult> CreateAsync(
        string projectId,
        CreateSprintProposalRequest request,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 获取提案详情，包含来源材料、对话和转需求记录。
    /// en-us: Gets proposal detail including source materials, conversations, and requirement conversion records.
    /// </summary>
    Task<SprintProposalResult> GetAsync(
        string proposalId,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 更新提案草稿内容和来源材料。
    /// en-us: Updates proposal draft content and source materials.
    /// </summary>
    Task<SprintProposalResult> UpdateAsync(
        string proposalId,
        UpdateSprintProposalRequest request,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 确认提案，进入后续转需求准备状态。
    /// en-us: Confirms a proposal so it is ready for later requirement conversion.
    /// </summary>
    Task<SprintProposalResult> ConfirmAsync(
        string proposalId,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 作废提案。
    /// en-us: Voids a proposal.
    /// </summary>
    Task<SprintProposalResult> VoidAsync(
        string proposalId,
        string userId,
        bool isSuperAdministrator);
}
