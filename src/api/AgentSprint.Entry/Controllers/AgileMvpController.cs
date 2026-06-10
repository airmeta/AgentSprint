using System.Security.Claims;

using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Route("mvp")]
public sealed class AgileMvpController : ControllerBase
{
    private readonly IAgileMvpService _agileMvpService;

    /// <summary>
    /// zh-cn: 创建敏捷 MVP 控制器，暴露项目、需求、租约、Bug 和闭环统计接口。
    /// en-us: Creates the agile MVP controller exposing project, requirement, lease, bug, and loop-summary endpoints.
    /// </summary>
    /// <param name="agileMvpService">
    /// zh-cn: 敏捷 MVP 服务。
    /// en-us: Agile MVP service.
    /// </param>
    public AgileMvpController(IAgileMvpService agileMvpService)
    {
        _agileMvpService = agileMvpService;
    }

    /// <summary>
    /// zh-cn: 获取 MVP 工作台统计信息。
    /// en-us: Gets MVP workbench summary metrics.
    /// </summary>
    /// <returns>
    /// zh-cn: 统计信息响应。
    /// en-us: Summary response.
    /// </returns>
    [HttpGet("summary")]
    public async Task<ApiResponse<SprintMvpSummaryResult>> GetSummary()
    {
        return ApiResponse<SprintMvpSummaryResult>.Ok(await _agileMvpService.GetSummaryAsync());
    }

    /// <summary>
    /// zh-cn: 创建项目。
    /// en-us: Creates a project.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 项目创建参数。
    /// en-us: Project creation payload.
    /// </param>
    /// <returns>
    /// zh-cn: 创建结果。
    /// en-us: Creation result.
    /// </returns>
    [HttpPost("projects")]
    public async Task<ActionResult<ApiResponse<SprintProjectResult>>> CreateProject(CreateSprintProjectRequest request)
    {
        try
        {
            return ApiResponse<SprintProjectResult>.Ok(
                await _agileMvpService.CreateProjectAsync(request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProjectResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 查询项目列表。
    /// en-us: Lists projects.
    /// </summary>
    /// <returns>
    /// zh-cn: 项目列表响应。
    /// en-us: Project list response.
    /// </returns>
    [HttpGet("projects")]
    public async Task<ApiResponse<IReadOnlyList<SprintProjectResult>>> ListProjects()
    {
        return ApiResponse<IReadOnlyList<SprintProjectResult>>.Ok(await _agileMvpService.ListProjectsAsync());
    }

    /// <summary>
    /// zh-cn: 更新项目基础配置。
    /// en-us: Updates basic project configuration.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 项目标识。
    /// en-us: Project identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 项目名称、仓库地址和测试环境地址。
    /// en-us: Project name, repository URL, and test environment URL.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的项目。
    /// en-us: Updated project.
    /// </returns>
    [HttpPut("projects/{id}")]
    public async Task<ActionResult<ApiResponse<SprintProjectResult>>> UpdateProject(
        string id,
        UpdateSprintProjectRequest request)
    {
        try
        {
            return ApiResponse<SprintProjectResult>.Ok(await _agileMvpService.UpdateProjectAsync(id, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProjectResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 创建 Skill 配置，供项目构建和新建需求时选择。
    /// en-us: Creates a skill configuration that can be selected while building projects and creating requirements.
    /// </summary>
    /// <param name="request">
    /// zh-cn: Skill 编码、名称、类型、说明和内容；编码为空时由服务自动生成。
    /// en-us: Skill code, name, type, description, and content; the service generates the code when it is empty.
    /// </param>
    /// <returns>
    /// zh-cn: 创建后的 Skill 配置。
    /// en-us: Created skill configuration.
    /// </returns>
    [HttpPost("skills")]
    public async Task<ActionResult<ApiResponse<SprintSkillResult>>> CreateSkill(CreateSprintSkillRequest request)
    {
        try
        {
            return ApiResponse<SprintSkillResult>.Ok(await _agileMvpService.CreateSkillAsync(request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintSkillResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 查询 Skill 配置列表；项目和需求表单可使用 activeOnly 只加载启用项。
    /// en-us: Lists skill configurations; project and requirement forms can use activeOnly to load selectable active items only.
    /// </summary>
    /// <param name="activeOnly">
    /// zh-cn: 是否仅返回启用状态。
    /// en-us: Whether to return active skills only.
    /// </param>
    /// <returns>
    /// zh-cn: Skill 配置列表。
    /// en-us: Skill configuration list.
    /// </returns>
    [HttpGet("skills")]
    public async Task<ApiResponse<IReadOnlyList<SprintSkillResult>>> ListSkills(
        [FromQuery] bool activeOnly = false,
        [FromQuery] string? keyword = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null)
    {
        return ApiResponse<IReadOnlyList<SprintSkillResult>>.Ok(
            await _agileMvpService.ListSkillsAsync(activeOnly, keyword, type, status));
    }

    /// <summary>
    /// zh-cn: 更新 Skill 配置类型、内容和启用状态。
    /// en-us: Updates skill configuration type, content, and active state.
    /// </summary>
    /// <param name="id">
    /// zh-cn: Skill 标识。
    /// en-us: Skill identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 更新载荷。
    /// en-us: Update payload.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的 Skill 配置。
    /// en-us: Updated skill configuration.
    /// </returns>
    [HttpPut("skills/{id}")]
    public async Task<ActionResult<ApiResponse<SprintSkillResult>>> UpdateSkill(
        string id,
        UpdateSprintSkillRequest request)
    {
        try
        {
            return ApiResponse<SprintSkillResult>.Ok(await _agileMvpService.UpdateSkillAsync(id, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintSkillResult>.Error(ex.Message, 400));
        }
    }

    [HttpPost("endpoints")]
    public async Task<ActionResult<ApiResponse<SprintProjectEndpointResult>>> CreateEndpoint(
        CreateSprintProjectEndpointRequest request)
    {
        try
        {
            return ApiResponse<SprintProjectEndpointResult>.Ok(
                await _agileMvpService.CreateProjectEndpointAsync(request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProjectEndpointResult>.Error(ex.Message, 400));
        }
    }

    [HttpGet("endpoints")]
    public async Task<ApiResponse<IReadOnlyList<SprintProjectEndpointResult>>> ListEndpoints(
        [FromQuery] string? projectId)
    {
        return ApiResponse<IReadOnlyList<SprintProjectEndpointResult>>.Ok(
            await _agileMvpService.ListProjectEndpointsAsync(projectId));
    }

    [HttpPut("endpoints/{id}")]
    public async Task<ActionResult<ApiResponse<SprintProjectEndpointResult>>> UpdateEndpoint(
        string id,
        UpdateSprintProjectEndpointRequest request)
    {
        try
        {
            return ApiResponse<SprintProjectEndpointResult>.Ok(
                await _agileMvpService.UpdateProjectEndpointAsync(id, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProjectEndpointResult>.Error(ex.Message, 400));
        }
    }

    [HttpPost("modules")]
    public async Task<ActionResult<ApiResponse<SprintFeatureModuleResult>>> CreateModule(
        CreateSprintFeatureModuleRequest request)
    {
        try
        {
            return ApiResponse<SprintFeatureModuleResult>.Ok(
                await _agileMvpService.CreateFeatureModuleAsync(request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintFeatureModuleResult>.Error(ex.Message, 400));
        }
    }

    [HttpGet("modules")]
    public async Task<ApiResponse<IReadOnlyList<SprintFeatureModuleResult>>> ListModules(
        [FromQuery] string? projectId,
        [FromQuery] string? endpointId)
    {
        return ApiResponse<IReadOnlyList<SprintFeatureModuleResult>>.Ok(
            await _agileMvpService.ListFeatureModulesAsync(projectId, endpointId));
    }

    [HttpPut("modules/{id}")]
    public async Task<ActionResult<ApiResponse<SprintFeatureModuleResult>>> UpdateModule(
        string id,
        UpdateSprintFeatureModuleRequest request)
    {
        try
        {
            return ApiResponse<SprintFeatureModuleResult>.Ok(
                await _agileMvpService.UpdateFeatureModuleAsync(id, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintFeatureModuleResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 删除没有需求引用的功能模块；已有需求绑定时返回业务错误，避免破坏需求归属。
    /// en-us: Deletes a feature module that has no requirement references; modules already bound by requirements return a business error to preserve ownership.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 功能模块标识。
    /// en-us: Feature module identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 删除结果。
    /// en-us: Delete result.
    /// </returns>
    [HttpDelete("modules/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteModule(string id)
    {
        try
        {
            return ApiResponse<bool>.Ok(await _agileMvpService.DeleteFeatureModuleAsync(id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 创建需求。
    /// en-us: Creates a requirement.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 需求创建参数。
    /// en-us: Requirement creation payload.
    /// </param>
    /// <returns>
    /// zh-cn: 创建结果。
    /// en-us: Creation result.
    /// </returns>
    [HttpPost("requirements")]
    public async Task<ActionResult<ApiResponse<SprintRequirementResult>>> CreateRequirement(
        CreateSprintRequirementRequest request)
    {
        try
        {
            return ApiResponse<SprintRequirementResult>.Ok(
                await _agileMvpService.CreateRequirementAsync(request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintRequirementResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 查询需求列表。
    /// en-us: Lists requirements.
    /// </summary>
    /// <param name="projectId">
    /// zh-cn: 可选项目标识。
    /// en-us: Optional project identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 需求列表响应。
    /// en-us: Requirement list response.
    /// </returns>
    [HttpGet("requirements")]
    public async Task<ApiResponse<IReadOnlyList<SprintRequirementResult>>> ListRequirements(
        [FromQuery] string? projectId,
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] string? health)
    {
        return ApiResponse<IReadOnlyList<SprintRequirementResult>>.Ok(
            await _agileMvpService.ListRequirementsAsync(projectId, keyword, status, health));
    }

    /// <summary>
    /// zh-cn: 更新需求基础内容。
    /// en-us: Updates basic requirement content.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 更新参数。
    /// en-us: Update payload.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    [HttpPut("requirements/{id}")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> UpdateRequirement(
        string id,
        UpdateSprintRequirementRequest request)
    {
        return ExecuteRequirementAction(() => _agileMvpService.UpdateRequirementAsync(id, request, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 删除尚未提交立项推进的草稿需求。
    /// en-us: Deletes a draft requirement that has not been submitted for initiation.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 删除结果。
    /// en-us: Delete result.
    /// </returns>
    [HttpDelete("requirements/{id}")]
    public Task<ActionResult<ApiResponse<bool>>> DeleteDraftRequirement(string id)
    {
        return ExecuteBooleanAction(() => _agileMvpService.DeleteDraftRequirementAsync(id, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 提交需求评审。
    /// en-us: Submits a requirement for review.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 评审人列表。
    /// en-us: Reviewer list.
    /// </param>
    /// <returns>
    /// zh-cn: 提交后的需求。
    /// en-us: Submitted requirement.
    /// </returns>
    [HttpPost("requirements/{id}/submit-review")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> SubmitRequirementReview(
        string id,
        SubmitSprintRequirementReviewRequest request)
    {
        return ExecuteRequirementAction(() =>
            _agileMvpService.SubmitRequirementReviewAsync(id, request, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 查询当前登录人的待评审需求。
    /// en-us: Lists requirement reviews pending for the current signed-in user.
    /// </summary>
    /// <returns>
    /// zh-cn: 待我评审列表。
    /// en-us: My pending review list.
    /// </returns>
    [HttpGet("reviews/my-pending")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SprintRequirementReviewItemResult>>>> ListMyPendingReviews(
        [FromQuery] string? projectId,
        [FromQuery] string? status,
        [FromQuery] string? keyword)
    {
        try
        {
            return ApiResponse<IReadOnlyList<SprintRequirementReviewItemResult>>.Ok(
                await _agileMvpService.ListMyPendingReviewsAsync(GetUserId(), projectId, status, keyword));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<SprintRequirementReviewItemResult>>.Error(
                "Authentication is required.",
                401));
        }
    }

    /// <summary>
    /// zh-cn: 查询指定需求的全部评审记录。
    /// en-us: Lists all review records for a requirement.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 需求评审记录。
    /// en-us: Requirement review records.
    /// </returns>
    [HttpGet("requirements/{id}/reviews")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SprintRequirementReviewResult>>>> ListRequirementReviews(
        string id)
    {
        try
        {
            return ApiResponse<IReadOnlyList<SprintRequirementReviewResult>>.Ok(
                await _agileMvpService.ListRequirementReviewsAsync(id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<IReadOnlyList<SprintRequirementReviewResult>>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 为已测试或已完成需求记录产品回馈，回馈可继续转换为下一轮需求。
    /// en-us: Records product feedback for a tested or completed requirement, which can later be converted into a follow-up requirement.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 原始需求标识。
    /// en-us: Original requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 回馈标题和正文。
    /// en-us: Feedback title and body.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建的回馈记录。
    /// en-us: Created feedback record.
    /// </returns>
    [HttpPost("requirements/{id}/feedback")]
    public async Task<ActionResult<ApiResponse<SprintRequirementFeedbackResult>>> CreateRequirementFeedback(
        string id,
        CreateSprintRequirementFeedbackRequest request)
    {
        try
        {
            return ApiResponse<SprintRequirementFeedbackResult>.Ok(
                await _agileMvpService.CreateRequirementFeedbackAsync(id, request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintRequirementFeedbackResult>.Error(ex.Message, 400));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<SprintRequirementFeedbackResult>.Error("Authentication is required.", 401));
        }
    }

    /// <summary>
    /// zh-cn: 查询需求回馈记录，用于展示开放回馈、已转需求和关闭时间。
    /// en-us: Lists requirement feedback records for showing open feedback, converted requirements, and close time.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 回馈记录列表。
    /// en-us: Feedback record list.
    /// </returns>
    [HttpGet("requirements/{id}/feedback")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SprintRequirementFeedbackResult>>>> ListRequirementFeedback(
        string id)
    {
        try
        {
            return ApiResponse<IReadOnlyList<SprintRequirementFeedbackResult>>.Ok(
                await _agileMvpService.ListRequirementFeedbackAsync(id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<IReadOnlyList<SprintRequirementFeedbackResult>>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 将开放回馈转换为下一轮草稿需求，保留来源需求和来源回馈以形成连续闭环。
    /// en-us: Converts open feedback into a next-round draft requirement while preserving source requirement and feedback for a continuous loop.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 原始需求标识。
    /// en-us: Original requirement identifier.
    /// </param>
    /// <param name="feedbackId">
    /// zh-cn: 回馈标识。
    /// en-us: Feedback identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 后续需求标题、正文、优先级和干系人。
    /// en-us: Follow-up requirement title, body, priority, and stakeholders.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建的后续草稿需求。
    /// en-us: Created follow-up draft requirement.
    /// </returns>
    [HttpPost("requirements/{id}/feedback/{feedbackId}/convert")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> ConvertRequirementFeedback(
        string id,
        string feedbackId,
        ConvertSprintRequirementFeedbackRequest request)
    {
        return ExecuteRequirementAction(() =>
            _agileMvpService.ConvertRequirementFeedbackAsync(id, feedbackId, request, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 将需求下选择的回馈和优化建议统一转换为后续草稿需求，并由服务端统一追加备注与回写来源状态。
    /// en-us: Converts selected feedback and feature suggestions under a requirement into a follow-up draft requirement, appending remarks and updating source states in the service.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 来源需求标识。
    /// en-us: Source requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 新需求标题、正文、优先级、干系人、回馈标识、建议标识和追加备注。
    /// en-us: New requirement title, body, priority, stakeholders, feedback ids, suggestion ids, and appended remark.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建的后续草稿需求。
    /// en-us: Created follow-up draft requirement.
    /// </returns>
    [HttpPost("requirements/{id}/convert-sources")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> ConvertRequirementSources(
        string id,
        ConvertSprintRequirementSourcesRequest request)
    {
        return ExecuteRequirementAction(() =>
            _agileMvpService.ConvertRequirementSourcesAsync(id, request, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 当前项目参与人提交优化建议，可关联项目、端、模块和可选需求；提交后保持开放状态等待需求转化选择。
    /// en-us: Lets a current project participant submit a feature suggestion linked to project, endpoint, module, and optional requirement; new suggestions remain open for later conversion.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 优化建议归属和建议内容。
    /// en-us: Suggestion ownership and content.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建的优化建议记录。
    /// en-us: Created feature suggestion record.
    /// </returns>
    [HttpPost("suggestions")]
    public async Task<ActionResult<ApiResponse<SprintFeatureSuggestionResult>>> CreateFeatureSuggestion(
        CreateSprintFeatureSuggestionRequest request)
    {
        try
        {
            return ApiResponse<SprintFeatureSuggestionResult>.Ok(
                await _agileMvpService.CreateFeatureSuggestionAsync(request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintFeatureSuggestionResult>.Error(ex.Message, 400));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<SprintFeatureSuggestionResult>.Error("Authentication is required.", 401));
        }
    }

    /// <summary>
    /// zh-cn: 查询优化建议列表，支持按项目、模块和需求过滤，用于需求转化时选择可处理来源。
    /// en-us: Lists feature suggestions with project, module, and requirement filters so conversion forms can select processable sources.
    /// </summary>
    /// <param name="projectId">
    /// zh-cn: 可选项目过滤。
    /// en-us: Optional project filter.
    /// </param>
    /// <param name="moduleId">
    /// zh-cn: 可选模块过滤。
    /// en-us: Optional module filter.
    /// </param>
    /// <param name="requirementId">
    /// zh-cn: 可选需求过滤。
    /// en-us: Optional requirement filter.
    /// </param>
    /// <returns>
    /// zh-cn: 优化建议记录列表。
    /// en-us: Feature suggestion records.
    /// </returns>
    [HttpGet("suggestions")]
    public async Task<ApiResponse<IReadOnlyList<SprintFeatureSuggestionResult>>> ListFeatureSuggestions(
        [FromQuery] string? projectId,
        [FromQuery] string? moduleId,
        [FromQuery] string? requirementId)
    {
        return ApiResponse<IReadOnlyList<SprintFeatureSuggestionResult>>.Ok(
            await _agileMvpService.ListFeatureSuggestionsAsync(projectId, moduleId, requirementId));
    }

    /// <summary>
    /// zh-cn: 通过当前登录人的需求评审。
    /// en-us: Approves the current user's requirement review.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 评审意见。
    /// en-us: Review comment.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    [HttpPost("requirements/{id}/review/approve")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> ApproveRequirementReview(
        string id,
        DecideSprintRequirementReviewRequest request)
    {
        return ExecuteRequirementAction(() =>
            _agileMvpService.ApproveRequirementReviewAsync(id, GetUserId(), request));
    }

    /// <summary>
    /// zh-cn: 驳回当前登录人的需求评审。
    /// en-us: Rejects the current user's requirement review.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 驳回意见。
    /// en-us: Rejection comment.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    [HttpPost("requirements/{id}/review/reject")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> RejectRequirementReview(
        string id,
        DecideSprintRequirementReviewRequest request)
    {
        return ExecuteRequirementAction(() =>
            _agileMvpService.RejectRequirementReviewAsync(id, GetUserId(), request));
    }

    /// <summary>
    /// zh-cn: 评审通过需求。
    /// en-us: Approves a requirement.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新结果。
    /// en-us: Update result.
    /// </returns>
    [HttpPost("requirements/{id}/approve")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> ApproveRequirement(string id)
    {
        return ExecuteRequirementAction(() => _agileMvpService.ApproveRequirementAsync(id, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 由当前登录人作废自己创建且已驳回的需求。
    /// en-us: Voids a rejected requirement created by the current signed-in user.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 作废后的需求。
    /// en-us: Voided requirement.
    /// </returns>
    [HttpPost("requirements/{id}/void")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> VoidRequirement(string id)
    {
        return ExecuteRequirementAction(() => _agileMvpService.VoidRequirementAsync(id, GetUserId()));
    }

    /// <summary>
    /// zh-cn: 对需求执行 AI 任务拆解并保存任务明细。
    /// en-us: Decomposes a requirement into development tasks and persists the generated details.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 拆解补充指令、指派模式、可选任务数量，以及手动指派时选择的研发人员；未配置任务数量时默认只生成 1 条任务。
    /// en-us: Decomposition instruction, assignment mode, optional task count, and the selected developer for manual assignment; when task count is not configured, one task is generated by default.
    /// </param>
    /// <returns>
    /// zh-cn: 拆解任务列表。
    /// en-us: Decomposed task list.
    /// </returns>
    [HttpPost("requirements/{id}/decompose")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>>> DecomposeRequirement(
        string id,
        DecomposeSprintRequirementRequest request)
    {
        try
        {
            return ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>.Ok(
                await _agileMvpService.DecomposeRequirementAsync(id, request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 查询任务大厅任务；超级管理员可查看全部任务，架构师、产品经理和项目经理仅查看自己参与项目内的任务，其他角色只能查看指派给自己的任务。
    /// en-us: Lists development tasks for the task hall; super administrators can view all tasks, architects, product managers, and project managers are scoped to participating projects, while other roles are restricted to their own assigned tasks.
    /// </summary>
    /// <param name="projectId">
    /// zh-cn: 可选项目标识。
    /// en-us: Optional project identifier.
    /// </param>
    /// <param name="requirementId">
    /// zh-cn: 可选需求标识。
    /// en-us: Optional requirement identifier.
    /// </param>
    /// <param name="assigneeId">
    /// zh-cn: 可选负责人标识。
    /// en-us: Optional assignee identifier.
    /// </param>
    /// <param name="relatedUserId">
    /// zh-cn: 可选关联人员标识，负责人或指派人任一匹配即返回。
    /// en-us: Optional related user identifier; returns tasks where either assignee or assigner matches.
    /// </param>
    /// <returns>
    /// zh-cn: 开发任务列表。
    /// en-us: Development task list.
    /// </returns>
    [HttpGet("tasks")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>>> ListDevelopmentTasks(
        [FromQuery] string? projectId,
        [FromQuery] string? requirementId,
        [FromQuery] string? assigneeId,
        [FromQuery] string? relatedUserId,
        [FromQuery] string? status,
        [FromQuery] bool primaryOnly = false)
    {
        try
        {
            if (IsSuperAdministrator() || IsProjectManager())
            {
                return ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>.Ok(
                    await _agileMvpService.ListDevelopmentTasksAsync(
                        projectId,
                        requirementId,
                        assigneeId,
                        relatedUserId,
                        status));
            }

            if (CanManageDevelopmentTasks())
            {
                return ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>.Ok(
                    await _agileMvpService.ListParticipatingDevelopmentTasksAsync(
                        projectId,
                        requirementId,
                        assigneeId,
                        relatedUserId,
                        status,
                        primaryOnly,
                        GetUserId()));
            }

            return ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>.Ok(
                await _agileMvpService.ListDevelopmentTasksAsync(
                    projectId,
                    requirementId,
                    GetUserId(),
                    null,
                    status));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>.Error(
                "Authentication is required.",
                401));
        }
    }

    /// <summary>
    /// zh-cn: 查询当前登录人的任务。
    /// en-us: Lists development tasks assigned to the current signed-in user.
    /// </summary>
    /// <returns>
    /// zh-cn: 我的任务列表。
    /// en-us: My task list.
    /// </returns>
    [HttpGet("tasks/my")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>>> ListMyDevelopmentTasks(
        [FromQuery] string? projectId,
        [FromQuery] string? requirementId,
        [FromQuery] string? status)
    {
        try
        {
            return ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>.Ok(
                await _agileMvpService.ListDevelopmentTasksAsync(projectId, requirementId, GetUserId(), null, status));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<SprintDevelopmentTaskResult>>.Error(
                "Authentication is required.",
                401));
        }
    }

    /// <summary>
    /// zh-cn: 指派开发任务。
    /// en-us: Assigns a development task.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 任务标识。
    /// en-us: Task identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 指派参数。
    /// en-us: Assignment payload.
    /// </param>
    /// <returns>
    /// zh-cn: 指派后的任务。
    /// en-us: Assigned task.
    /// </returns>
    [HttpPost("tasks/{id}/assign")]
    public async Task<ActionResult<ApiResponse<SprintDevelopmentTaskResult>>> AssignDevelopmentTask(
        string id,
        AssignSprintDevelopmentTaskRequest request)
    {
        try
        {
            EnsureTaskAssignmentRole();
            return ApiResponse<SprintDevelopmentTaskResult>.Ok(
                await _agileMvpService.AssignDevelopmentTaskAsync(id, request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintDevelopmentTaskResult>.Error(ex.Message, 400));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<SprintDevelopmentTaskResult>.Error("Authentication is required.", 401));
        }
        catch (UnauthorizedRoleException ex)
        {
            return StatusCode(403, ApiResponse<SprintDevelopmentTaskResult>.Error(ex.Message, 403));
        }
    }

    /// <summary>
    /// zh-cn: 生成当前登录人任务推进提示词。
    /// en-us: Generates a task continuation prompt for the current signed-in user.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 任务标识。
    /// en-us: Task identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 任务推进提示词。
    /// en-us: Task continuation prompt.
    /// </returns>
    [HttpGet("tasks/{id}/prompt")]
    public async Task<ActionResult<ApiResponse<SprintTaskPromptResult>>> GetDevelopmentTaskPrompt(string id)
    {
        try
        {
            return ApiResponse<SprintTaskPromptResult>.Ok(
                await _agileMvpService.GetDevelopmentTaskPromptAsync(id, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintTaskPromptResult>.Error(ex.Message, 400));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<SprintTaskPromptResult>.Error("Authentication is required.", 401));
        }
    }

    /// <summary>
    /// zh-cn: 当前负责人完成拆解任务。
    /// en-us: Completes a decomposed task for the current assignee.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 任务标识。
    /// en-us: Task identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 完成后的任务。
    /// en-us: Completed task.
    /// </returns>
    [HttpPost("tasks/{id}/complete")]
    public async Task<ActionResult<ApiResponse<SprintDevelopmentTaskResult>>> CompleteDevelopmentTask(string id)
    {
        try
        {
            return ApiResponse<SprintDevelopmentTaskResult>.Ok(
                await _agileMvpService.CompleteDevelopmentTaskAsync(id, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintDevelopmentTaskResult>.Error(ex.Message, 400));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<SprintDevelopmentTaskResult>.Error("Authentication is required.", 401));
        }
    }

    /// <summary>
    /// zh-cn: 当前负责人领取开发任务并创建会话租约。
    /// en-us: Claims a development task for the current assignee and creates a session lease.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 任务标识。
    /// en-us: Task identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 领取参数。
    /// en-us: Claim payload.
    /// </param>
    /// <returns>
    /// zh-cn: 任务租约。
    /// en-us: Task lease.
    /// </returns>
    [HttpPost("tasks/{id}/claim")]
    public async Task<ActionResult<ApiResponse<SprintTaskLeaseResult>>> ClaimDevelopmentTask(
        string id,
        ClaimSprintTaskRequest request)
    {
        try
        {
            return ApiResponse<SprintTaskLeaseResult>.Ok(
                await _agileMvpService.ClaimDevelopmentTaskAsync(id, request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintTaskLeaseResult>.Error(ex.Message, 400));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<SprintTaskLeaseResult>.Error("Authentication is required.", 401));
        }
    }

    /// <summary>
    /// zh-cn: 开发领取需求。
    /// en-us: Claims a requirement for development.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 领取参数。
    /// en-us: Claim payload.
    /// </param>
    /// <returns>
    /// zh-cn: 任务租约。
    /// en-us: Task lease.
    /// </returns>
    [HttpPost("requirements/{id}/claim")]
    public async Task<ActionResult<ApiResponse<SprintTaskLeaseResult>>> ClaimRequirement(
        string id,
        ClaimSprintTaskRequest request)
    {
        try
        {
            return ApiResponse<SprintTaskLeaseResult>.Ok(
                await _agileMvpService.ClaimRequirementAsync(id, request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintTaskLeaseResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 标记需求开发完成。
    /// en-us: Marks requirement development complete.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 开发完成参数。
    /// en-us: Development completion payload.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    [HttpPost("requirements/{id}/complete-development")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> CompleteRequirementDevelopment(
        string id,
        CompleteSprintDevelopmentRequest request)
    {
        return ExecuteRequirementAction(() => _agileMvpService.CompleteRequirementDevelopmentAsync(id, request));
    }

    /// <summary>
    /// zh-cn: 标记需求测试中。
    /// en-us: Marks a requirement as testing.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    [HttpPost("requirements/{id}/start-testing")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> StartRequirementTesting(string id)
    {
        return ExecuteRequirementAction(() => _agileMvpService.StartRequirementTestingAsync(id));
    }

    /// <summary>
    /// zh-cn: 标记需求测试通过。
    /// en-us: Marks a requirement as tested.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    [HttpPost("requirements/{id}/mark-tested")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> MarkRequirementTested(string id)
    {
        return ExecuteRequirementAction(() => _agileMvpService.MarkRequirementTestedAsync(id));
    }

    /// <summary>
    /// zh-cn: 关闭需求。
    /// en-us: Closes a requirement.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    [HttpPost("requirements/{id}/close")]
    public Task<ActionResult<ApiResponse<SprintRequirementResult>>> CloseRequirement(string id)
    {
        return ExecuteRequirementAction(() => _agileMvpService.CloseRequirementAsync(id));
    }

    /// <summary>
    /// zh-cn: 创建 Bug。
    /// en-us: Creates a bug.
    /// </summary>
    /// <param name="request">
    /// zh-cn: Bug 创建参数。
    /// en-us: Bug creation payload.
    /// </param>
    /// <returns>
    /// zh-cn: 创建结果。
    /// en-us: Creation result.
    /// </returns>
    [HttpPost("bugs")]
    public async Task<ActionResult<ApiResponse<SprintBugResult>>> CreateBug(CreateSprintBugRequest request)
    {
        try
        {
            return ApiResponse<SprintBugResult>.Ok(await _agileMvpService.CreateBugAsync(request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintBugResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 查询 Bug 列表。
    /// en-us: Lists bugs.
    /// </summary>
    /// <param name="projectId">
    /// zh-cn: 可选项目标识。
    /// en-us: Optional project identifier.
    /// </param>
    /// <param name="requirementId">
    /// zh-cn: 可选需求标识。
    /// en-us: Optional requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: Bug 列表响应。
    /// en-us: Bug list response.
    /// </returns>
    [HttpGet("bugs")]
    public async Task<ApiResponse<IReadOnlyList<SprintBugResult>>> ListBugs(
        [FromQuery] string? projectId,
        [FromQuery] string? requirementId)
    {
        return ApiResponse<IReadOnlyList<SprintBugResult>>.Ok(
            await _agileMvpService.ListBugsAsync(projectId, requirementId));
    }

    /// <summary>
    /// zh-cn: 开发领取 Bug。
    /// en-us: Claims a bug for fixing.
    /// </summary>
    /// <param name="id">
    /// zh-cn: Bug 标识。
    /// en-us: Bug identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 领取参数。
    /// en-us: Claim payload.
    /// </param>
    /// <returns>
    /// zh-cn: 任务租约。
    /// en-us: Task lease.
    /// </returns>
    [HttpPost("bugs/{id}/claim")]
    public async Task<ActionResult<ApiResponse<SprintTaskLeaseResult>>> ClaimBug(
        string id,
        ClaimSprintTaskRequest request)
    {
        try
        {
            return ApiResponse<SprintTaskLeaseResult>.Ok(
                await _agileMvpService.ClaimBugAsync(id, request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintTaskLeaseResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 标记 Bug 已修复。
    /// en-us: Marks a bug as fixed.
    /// </summary>
    /// <param name="id">
    /// zh-cn: Bug 标识。
    /// en-us: Bug identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的 Bug。
    /// en-us: Updated bug.
    /// </returns>
    [HttpPost("bugs/{id}/fix")]
    public async Task<ActionResult<ApiResponse<SprintBugResult>>> FixBug(string id)
    {
        try
        {
            return ApiResponse<SprintBugResult>.Ok(await _agileMvpService.FixBugAsync(id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintBugResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 关闭已修复待回归的 Bug。
    /// en-us: Closes a fixed bug after regression.
    /// </summary>
    /// <param name="id">
    /// zh-cn: Bug 标识。
    /// en-us: Bug identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 关闭后的 Bug。
    /// en-us: Closed bug.
    /// </returns>
    [HttpPost("bugs/{id}/close")]
    public async Task<ActionResult<ApiResponse<SprintBugResult>>> CloseBug(string id)
    {
        try
        {
            return ApiResponse<SprintBugResult>.Ok(await _agileMvpService.CloseBugAsync(id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintBugResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 查询活跃租约。
    /// en-us: Lists active leases.
    /// </summary>
    /// <returns>
    /// zh-cn: 活跃租约列表。
    /// en-us: Active lease list.
    /// </returns>
    [HttpGet("leases/active")]
    public async Task<ApiResponse<IReadOnlyList<SprintTaskLeaseResult>>> ListActiveLeases()
    {
        return ApiResponse<IReadOnlyList<SprintTaskLeaseResult>>.Ok(
            await _agileMvpService.ListActiveLeasesAsync());
    }

    private async Task<ActionResult<ApiResponse<SprintRequirementResult>>> ExecuteRequirementAction(
        Func<Task<SprintRequirementResult>> action)
    {
        try
        {
            return ApiResponse<SprintRequirementResult>.Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintRequirementResult>.Error(ex.Message, 400));
        }
    }

    private async Task<ActionResult<ApiResponse<bool>>> ExecuteBooleanAction(Func<Task<bool>> action)
    {
        try
        {
            return ApiResponse<bool>.Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.Error(ex.Message, 400));
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    }

    private void EnsureTaskAssignmentRole()
    {
        if (IsSuperAdministrator() || CanManageDevelopmentTasks())
        {
            return;
        }

        throw new UnauthorizedRoleException("Only architect, product manager or project manager can assign tasks.");
    }

    private bool IsSuperAdministrator()
    {
        return GetRoleSet().Contains("super");
    }

    private bool CanManageDevelopmentTasks()
    {
        var roles = GetRoleSet();
        return roles.Contains("pm") ||
            roles.Contains("architect") ||
            roles.Contains("project_manager");
    }

    private bool IsProjectManager()
    {
        return GetRoleSet().Contains("project_manager");
    }

    private HashSet<string> GetRoleSet()
    {
        return User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
    }

    private sealed class UnauthorizedRoleException(string message) : Exception(message);
}
