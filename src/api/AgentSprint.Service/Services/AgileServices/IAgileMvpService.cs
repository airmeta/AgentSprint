using AgentSprint.Model.Modules.Agile.Dtos;

namespace AgentSprint.Service.Services.AgileServices;

public interface IAgileMvpService
{
    /// <summary>
    /// zh-cn: 创建可进入 MVP 闭环的项目基础资料。
    /// en-us: Creates the basic project record used by the MVP delivery loop.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 项目标识、名称、仓库和测试环境地址。
    /// en-us: Project code, name, repository, and test environment URL.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前登录用户标识，写入 CreatedBy。
    /// en-us: Current authenticated user identifier written to CreatedBy.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建项目。
    /// en-us: Created project.
    /// </returns>
    Task<SprintProjectResult> CreateProjectAsync(CreateSprintProjectRequest request, string userId);

    /// <summary>
    /// zh-cn: 查询项目列表，按创建时间倒序返回未删除项目。
    /// en-us: Lists non-deleted projects ordered by creation time descending.
    /// </summary>
    /// <returns>
    /// zh-cn: 项目列表。
    /// en-us: Project list.
    /// </returns>
    Task<IReadOnlyList<SprintProjectResult>> ListProjectsAsync();

    /// <summary>
    /// zh-cn: 更新项目基础配置，保留项目编码和创建人不变，用于项目管理抽屉编辑仓库地址和测试环境。
    /// en-us: Updates basic project configuration while preserving the project code and creator, so the project drawer can edit repository and test environment settings.
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
    Task<SprintProjectResult> UpdateProjectAsync(string id, UpdateSprintProjectRequest request);

    /// <summary>
    /// zh-cn: 创建可在项目和需求中选择的 Skill 配置，编码全局唯一且内容用于后续任务提示词上下文。
    /// en-us: Creates a selectable skill configuration for projects and requirements; the code is globally unique and the content is used by later task-prompt context.
    /// </summary>
    /// <param name="request">
    /// zh-cn: Skill 编码、名称、说明和指令内容。
    /// en-us: Skill code, name, description, and instruction content.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前创建人标识，写入 CreatedBy。
    /// en-us: Current creator identifier written to CreatedBy.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建的 Skill 配置。
    /// en-us: Created skill configuration.
    /// </returns>
    Task<SprintSkillResult> CreateSkillAsync(CreateSprintSkillRequest request, string userId);

    /// <summary>
    /// zh-cn: 查询 Skill 配置列表，可选择仅返回启用状态，用于项目构建和新建需求时的下拉选择。
    /// en-us: Lists skill configurations and can optionally return only active items for project-building and requirement-creation selectors.
    /// </summary>
    /// <param name="activeOnly">
    /// zh-cn: 为 true 时只返回启用状态的 Skill。
    /// en-us: When true, only active skills are returned.
    /// </param>
    /// <returns>
    /// zh-cn: Skill 配置列表。
    /// en-us: Skill configuration list.
    /// </returns>
    Task<IReadOnlyList<SprintSkillResult>> ListSkillsAsync(bool activeOnly = false);

    /// <summary>
    /// zh-cn: 更新 Skill 名称、说明、内容和状态；停用后的 Skill 不再允许被新项目或新需求选择。
    /// en-us: Updates skill name, description, content, and status; disabled skills can no longer be selected by new projects or requirements.
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
    Task<SprintSkillResult> UpdateSkillAsync(string id, UpdateSprintSkillRequest request);

    /// <summary>
    /// zh-cn: 鍒涘缓椤圭洰绔紝骞跺悓姝ョ璐熻矗浜恒€佸紑鍙戝拰娴嬭瘯浜哄憳涓洪」鐩垚鍛樸€?    /// en-us: Creates a project endpoint and syncs its owner, developers, and testers as project members.
    /// </summary>
    Task<SprintProjectEndpointResult> CreateProjectEndpointAsync(CreateSprintProjectEndpointRequest request, string userId);

    /// <summary>
    /// zh-cn: 鏌ヨ椤圭洰绔垪琛紝鐢ㄤ簬闇€姹傚綊灞炲拰妯″潡缁存姢銆?    /// en-us: Lists project endpoints for requirement ownership and module maintenance.
    /// </summary>
    Task<IReadOnlyList<SprintProjectEndpointResult>> ListProjectEndpointsAsync(string? projectId);

    /// <summary>
    /// zh-cn: 鏇存柊椤圭洰绔殑鍚嶇О銆佺被鍨嬨€佽礋璐ｄ汉鍜屼汉鍛橀厤缃€?    /// en-us: Updates endpoint name, type, owner, and staffing configuration.
    /// </summary>
    Task<SprintProjectEndpointResult> UpdateProjectEndpointAsync(string id, UpdateSprintProjectEndpointRequest request);

    /// <summary>
    /// zh-cn: 鍒涘缓绔笅鍔熻兘妯″潡锛岄渶姹傚拰寤鸿鍙粦瀹氬埌璇ユā鍧椼€?    /// en-us: Creates a feature module under an endpoint so requirements and suggestions can bind to it.
    /// </summary>
    Task<SprintFeatureModuleResult> CreateFeatureModuleAsync(CreateSprintFeatureModuleRequest request, string userId);

    /// <summary>
    /// zh-cn: 鏌ヨ鍔熻兘妯″潡鍒楄〃锛屽彲鎸夐」鐩垨绔瓫閫夈€?    /// en-us: Lists feature modules filtered by project or endpoint.
    /// </summary>
    Task<IReadOnlyList<SprintFeatureModuleResult>> ListFeatureModulesAsync(string? projectId, string? endpointId);

    /// <summary>
    /// zh-cn: 鏇存柊鍔熻兘妯″潡淇℃伅鍜屼汉鍛橀厤缃紝妯″潡浜哄憳浼樺厛浜庣鍜岄」鐩汉鍛樸€?    /// en-us: Updates module information and staffing; module staffing takes precedence over endpoint and project staffing.
    /// </summary>
    Task<SprintFeatureModuleResult> UpdateFeatureModuleAsync(string id, UpdateSprintFeatureModuleRequest request);

    /// <summary>
    /// zh-cn: 创建需求并进入待项目经理评审状态。
    /// en-us: Creates a requirement and moves it into pending review.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 需求所属项目、标题、描述和优先级。
    /// en-us: Owning project, title, description, and priority.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前创建人标识。
    /// en-us: Current creator identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建需求。
    /// en-us: Created requirement.
    /// </returns>
    Task<SprintRequirementResult> CreateRequirementAsync(CreateSprintRequirementRequest request, string userId);

    /// <summary>
    /// zh-cn: 更新草稿、驳回或待评审之前的需求基础内容，保留需求和项目的既有关联。
    /// en-us: Updates basic requirement content before or after review rejection while preserving its existing project linkage.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 标题、Markdown 正文、优先级和干系人。
    /// en-us: Title, Markdown body, priority, and stakeholders.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    Task<SprintRequirementResult> UpdateRequirementAsync(
        string id,
        UpdateSprintRequirementRequest request,
        string userId);

    /// <summary>
    /// zh-cn: 删除尚未提交立项推进的草稿需求，仅允许需求创建人操作，删除后需求会从常规列表中隐藏。
    /// en-us: Deletes a draft requirement that has not been submitted for initiation, allowing only the requirement creator and hiding the requirement from normal lists.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前登录用户标识，用于校验需求创建人身份。
    /// en-us: Current signed-in user identifier used to validate requirement ownership.
    /// </param>
    /// <returns>
    /// zh-cn: 删除是否成功。
    /// en-us: Whether the delete operation succeeded.
    /// </returns>
    Task<bool> DeleteDraftRequirementAsync(string id, string userId);

    /// <summary>
    /// zh-cn: 查询需求列表，可按项目筛选。
    /// en-us: Lists requirements with an optional project filter.
    /// </summary>
    /// <param name="projectId">
    /// zh-cn: 可选项目标识。
    /// en-us: Optional project identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 需求列表。
    /// en-us: Requirement list.
    /// </returns>
    Task<IReadOnlyList<SprintRequirementResult>> ListRequirementsAsync(string? projectId);

    /// <summary>
    /// zh-cn: 为已测试或已完成需求记录产品回馈，用于把验收后的新想法、补充范围或优化建议纳入下一轮需求闭环。
    /// en-us: Records product feedback for a tested or completed requirement so new ideas, added scope, or improvements after acceptance can enter the next requirement loop.
    /// </summary>
    /// <param name="requirementId">
    /// zh-cn: 原始需求标识。
    /// en-us: Original requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 回馈标题和正文。
    /// en-us: Feedback title and body.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前提交回馈的产品或项目成员标识。
    /// en-us: Current product or project member identifier submitting the feedback.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建的回馈记录。
    /// en-us: Created feedback record.
    /// </returns>
    Task<SprintRequirementFeedbackResult> CreateRequirementFeedbackAsync(
        string requirementId,
        CreateSprintRequirementFeedbackRequest request,
        string userId);

    /// <summary>
    /// zh-cn: 查询需求的全部回馈记录，按创建时间倒序返回，便于产品经理追踪已转需求和待处理回馈。
    /// en-us: Lists all feedback records for a requirement in descending creation order so product managers can track converted and open feedback.
    /// </summary>
    /// <param name="requirementId">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 回馈记录列表。
    /// en-us: Feedback record list.
    /// </returns>
    Task<IReadOnlyList<SprintRequirementFeedbackResult>> ListRequirementFeedbackAsync(string requirementId);

    /// <summary>
    /// zh-cn: 将开放状态的回馈转换为下一轮草稿需求，并在新需求上保留原需求和回馈来源，随后可继续提交评审形成无限闭环。
    /// en-us: Converts open feedback into a next-round draft requirement while retaining the source requirement and feedback, after which it can be submitted for review to form a continuous loop.
    /// </summary>
    /// <param name="requirementId">
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
    /// <param name="userId">
    /// zh-cn: 当前转换回馈的用户标识，写入新需求创建人。
    /// en-us: Current user identifier converting the feedback and written as the new requirement creator.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建的后续草稿需求。
    /// en-us: Created follow-up draft requirement.
    /// </returns>
    Task<SprintRequirementResult> ConvertRequirementFeedbackAsync(
        string requirementId,
        string feedbackId,
        ConvertSprintRequirementFeedbackRequest request,
        string userId);

    /// <summary>
    /// zh-cn: 将需求下选择的开放回馈和优化建议统一转换为下一轮草稿需求，按需追加备注，并回写来源处理状态。
    /// en-us: Converts selected open feedback and feature suggestions under a requirement into a follow-up draft requirement, appends an optional remark, and updates source processing status.
    /// </summary>
    /// <param name="requirementId">
    /// zh-cn: 来源需求标识，所有选择的回馈和建议都必须归属或关联到该需求所在项目范围。
    /// en-us: Source requirement id; every selected feedback and suggestion must belong to or be linked within the same project scope.
    /// </param>
    /// <param name="request">
    /// zh-cn: 新需求标题、正文、优先级、干系人、来源回馈、来源建议和追加备注。
    /// en-us: New requirement title, body, priority, stakeholders, selected feedback, selected suggestions, and appended remark.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前执行转化的用户标识，写入新需求创建人并维护项目成员身份。
    /// en-us: Current converter user id, stored as the new requirement creator and used to maintain project membership.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建的后续草稿需求。
    /// en-us: Created follow-up draft requirement.
    /// </returns>
    Task<SprintRequirementResult> ConvertRequirementSourcesAsync(
        string requirementId,
        ConvertSprintRequirementSourcesRequest request,
        string userId);

    /// <summary>
    /// zh-cn: 椤圭洰鍙備笌浜烘彁浜ゅ姛鑳藉缓璁紝鍙叧鑱旈」鐩€佺銆佹ā鍧楀拰鍙€夐渶姹傘€?    /// en-us: Lets project participants submit feature suggestions linked to a project, endpoint, module, and optional requirement.
    /// </summary>
    Task<SprintFeatureSuggestionResult> CreateFeatureSuggestionAsync(
        CreateSprintFeatureSuggestionRequest request,
        string userId);

    /// <summary>
    /// zh-cn: 鏌ヨ鍔熻兘寤鸿鍒楄〃锛屾敮鎸侀」鐩€佹ā鍧楀拰闇€姹傜瓫閫夈€?    /// en-us: Lists feature suggestions with project, module, and requirement filters.
    /// </summary>
    Task<IReadOnlyList<SprintFeatureSuggestionResult>> ListFeatureSuggestionsAsync(
        string? projectId,
        string? moduleId,
        string? requirementId);

    /// <summary>
    /// zh-cn: 将需求提交到评审流程，并为选择的评审人创建待评审记录。
    /// en-us: Submits a requirement into review and creates pending review records for the selected reviewers.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 评审人列表。
    /// en-us: Reviewer identifier list.
    /// </param>
    /// <returns>
    /// zh-cn: 提交后的需求。
    /// en-us: Submitted requirement.
    /// </returns>
    Task<SprintRequirementResult> SubmitRequirementReviewAsync(
        string id,
        SubmitSprintRequirementReviewRequest request,
        string userId);

    /// <summary>
    /// zh-cn: 查询当前评审人待处理的需求评审列表。
    /// en-us: Lists requirement reviews waiting for the current reviewer.
    /// </summary>
    /// <param name="reviewerId">
    /// zh-cn: 当前评审人标识。
    /// en-us: Current reviewer identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 待我评审项。
    /// en-us: Items awaiting my review.
    /// </returns>
    Task<IReadOnlyList<SprintRequirementReviewItemResult>> ListMyPendingReviewsAsync(string reviewerId);

    /// <summary>
    /// zh-cn: 查询指定需求的全部评审记录，用于需求详情展示评审进度和驳回意见。
    /// en-us: Lists all review records for a requirement so detail views can show review progress and rejection comments.
    /// </summary>
    /// <param name="requirementId">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 按创建时间排序的评审记录。
    /// en-us: Review records ordered by creation time.
    /// </returns>
    Task<IReadOnlyList<SprintRequirementReviewResult>> ListRequirementReviewsAsync(string requirementId);

    /// <summary>
    /// zh-cn: 当前评审人通过需求评审；当所有评审人通过后，需求进入评审通过状态。
    /// en-us: Approves the current reviewer's review; when all reviewers approve, the requirement enters the approved state.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前评审人标识。
    /// en-us: Current reviewer identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 评审意见。
    /// en-us: Review comment.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    Task<SprintRequirementResult> ApproveRequirementReviewAsync(
        string id,
        string userId,
        DecideSprintRequirementReviewRequest request);

    /// <summary>
    /// zh-cn: 当前评审人驳回需求评审，需求回到产品经理处理。
    /// en-us: Rejects a requirement review for the current reviewer and returns the requirement to the product owner.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前评审人标识。
    /// en-us: Current reviewer identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 驳回意见。
    /// en-us: Rejection comment.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    Task<SprintRequirementResult> RejectRequirementReviewAsync(
        string id,
        string userId,
        DecideSprintRequirementReviewRequest request);

    /// <summary>
    /// zh-cn: 由需求创建人作废已驳回需求，保留审计状态但不再进入后续交付。
    /// en-us: Lets the requirement creator void a rejected requirement, preserving audit state while excluding it from later delivery.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前登录用户标识，必须与需求创建人一致。
    /// en-us: Current signed-in user identifier, which must match the requirement creator.
    /// </param>
    /// <returns>
    /// zh-cn: 作废后的需求。
    /// en-us: Voided requirement.
    /// </returns>
    Task<SprintRequirementResult> VoidRequirementAsync(string id, string userId);

    /// <summary>
    /// zh-cn: 调用最小 AI 拆解逻辑生成需求任务明细并保存。
    /// en-us: Runs the minimal AI-like decomposition logic to generate and persist development tasks for a requirement.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 拆解补充指令、指派模式和可选任务数量；未配置任务数量时默认只生成 1 条任务。
    /// en-us: Decomposition instruction, assignment mode, and optional task count; when task count is not configured, one task is generated by default.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前操作者标识。
    /// en-us: Current operator identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 已生成任务列表。
    /// en-us: Generated task list.
    /// </returns>
    Task<IReadOnlyList<SprintDevelopmentTaskResult>> DecomposeRequirementAsync(
        string id,
        DecomposeSprintRequirementRequest request,
        string userId);

    /// <summary>
    /// zh-cn: 查询任务大厅任务，可按项目、需求、负责人过滤。
    /// en-us: Lists task-hall tasks with optional project, requirement, and assignee filters.
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
    /// <returns>
    /// zh-cn: 开发任务列表。
    /// en-us: Development-task list.
    /// </returns>
    Task<IReadOnlyList<SprintDevelopmentTaskResult>> ListDevelopmentTasksAsync(
        string? projectId,
        string? requirementId,
        string? assigneeId,
        string? status = null);

    /// <summary>
    /// zh-cn: 查询当前用户参与项目范围内的任务大厅任务，管理角色可在参与项目内按条件筛选，普通开发由控制器使用负责人过滤。
    /// en-us: Lists task-hall items within projects the current user participates in; manager roles may filter within those projects while normal developers are filtered by assignee at the controller.
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
    /// <param name="status">
    /// zh-cn: 可选任务状态。
    /// en-us: Optional task status.
    /// </param>
    /// <param name="primaryOnly">
    /// zh-cn: 是否仅返回当前用户作为模块、端或项目主要开发人员参与的任务；为空或 false 时返回全部参与项目任务。
    /// en-us: Whether to return only tasks where the current user is a primary developer on the module, endpoint, or project; null or false returns all participating-project tasks.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前登录用户标识，用于解析项目参与范围。
    /// en-us: Current signed-in user identifier used to resolve project participation scope.
    /// </param>
    /// <returns>
    /// zh-cn: 当前用户参与项目范围内的开发任务列表。
    /// en-us: Development tasks within the projects the user participates in.
    /// </returns>
    Task<IReadOnlyList<SprintDevelopmentTaskResult>> ListParticipatingDevelopmentTasksAsync(
        string? projectId,
        string? requirementId,
        string? assigneeId,
        string? status,
        bool primaryOnly,
        string userId);

    /// <summary>
    /// zh-cn: 将拆解任务指派给开发人员。
    /// en-us: Assigns a decomposed task to a developer.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 任务标识。
    /// en-us: Task identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 负责人标识。
    /// en-us: Assignee identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 指派后的任务。
    /// en-us: Assigned task.
    /// </returns>
    Task<SprintDevelopmentTaskResult> AssignDevelopmentTaskAsync(
        string id,
        AssignSprintDevelopmentTaskRequest request,
        string assignedBy);

    /// <summary>
    /// zh-cn: 为任务推进生成可复制到本地 Codex 的提示词。
    /// en-us: Generates a prompt that can be copied into local Codex to continue task execution.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 任务标识。
    /// en-us: Task identifier.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前开发人员标识，用于校验任务归属。
    /// en-us: Current developer identifier used to validate task ownership.
    /// </param>
    /// <returns>
    /// zh-cn: 任务推进提示词。
    /// en-us: Task continuation prompt.
    /// </returns>
    Task<SprintTaskPromptResult> GetDevelopmentTaskPromptAsync(string id, string userId);

    /// <summary>
    /// zh-cn: 当前负责人完成拆解任务；当同一需求下全部任务完成时，需求进入待测试状态。
    /// en-us: Completes a decomposed task for the current assignee; when all tasks under the same requirement are completed, the requirement moves to ready-for-test.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 任务标识。
    /// en-us: Task identifier.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前负责人标识，用于校验任务归属。
    /// en-us: Current assignee identifier used to validate task ownership.
    /// </param>
    /// <returns>
    /// zh-cn: 完成后的任务。
    /// en-us: Completed task.
    /// </returns>
    Task<SprintDevelopmentTaskResult> CompleteDevelopmentTaskAsync(string id, string userId);

    /// <summary>
    /// zh-cn: 为当前负责人领取一个已分配的开发任务并创建活跃任务租约，避免同一任务被多个 Codex 会话窗口同时推进。
    /// en-us: Claims an assigned development task for the current assignee and creates an active task lease so multiple Codex session windows do not advance the same task concurrently.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 任务标识。
    /// en-us: Task identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 领取参数，可包含本地设备或会话标识。
    /// en-us: Claim payload that can include the local device or session identifier.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前负责人标识，用于校验任务归属。
    /// en-us: Current assignee identifier used to validate task ownership.
    /// </param>
    /// <returns>
    /// zh-cn: 创建或复用的任务租约。
    /// en-us: Created or reused task lease.
    /// </returns>
    Task<SprintTaskLeaseResult> ClaimDevelopmentTaskAsync(string id, ClaimSprintTaskRequest request, string userId);

    /// <summary>
    /// zh-cn: 将已由所有评审人通过的需求推进到待开发队列；该方法不替代评审动作，不能绕过多人评审规则。
    /// en-us: Moves a requirement already approved by all reviewers into the ready-for-development queue; this does not replace review decisions and cannot bypass the multi-reviewer rule.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前推进人标识，记录到 ReviewedBy 以保留操作审计。
    /// en-us: Current operator identifier recorded in ReviewedBy for operation audit.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    Task<SprintRequirementResult> ApproveRequirementAsync(string id, string userId);

    /// <summary>
    /// zh-cn: 开发领取需求并生成活跃任务租约。
    /// en-us: Claims a requirement for development and creates an active task lease.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 可选设备标识，用于恢复任务。
    /// en-us: Optional device identifier used for task recovery.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前开发用户标识。
    /// en-us: Current developer identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 新建租约。
    /// en-us: Created lease.
    /// </returns>
    Task<SprintTaskLeaseResult> ClaimRequirementAsync(string id, ClaimSprintTaskRequest request, string userId);

    /// <summary>
    /// zh-cn: 开发完成需求，关闭对应活跃租约，并使需求进入待测试。
    /// en-us: Completes requirement development, closes its active lease, and moves the requirement to ready for test.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 可选测试环境地址。
    /// en-us: Optional test environment URL.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    Task<SprintRequirementResult> CompleteRequirementDevelopmentAsync(string id, CompleteSprintDevelopmentRequest request);

    /// <summary>
    /// zh-cn: 将待测试需求标记为测试中。
    /// en-us: Marks a ready-for-test requirement as testing.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    Task<SprintRequirementResult> StartRequirementTestingAsync(string id);

    /// <summary>
    /// zh-cn: 标记需求测试通过，等待产品或项目经理关闭。
    /// en-us: Marks a requirement as tested and ready for product or project-manager closure.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的需求。
    /// en-us: Updated requirement.
    /// </returns>
    Task<SprintRequirementResult> MarkRequirementTestedAsync(string id);

    /// <summary>
    /// zh-cn: 关闭已测试需求，完成 MVP 闭环。
    /// en-us: Closes a tested requirement and completes the MVP delivery loop.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 需求标识。
    /// en-us: Requirement identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 已关闭需求。
    /// en-us: Closed requirement.
    /// </returns>
    Task<SprintRequirementResult> CloseRequirementAsync(string id);

    /// <summary>
    /// zh-cn: 创建绑定需求的 Bug，并把需求推进到测试失败/待修复状态。
    /// en-us: Creates a requirement-bound bug and moves the requirement to test failed or pending fix.
    /// </summary>
    /// <param name="request">
    /// zh-cn: Bug 标题、描述、环境和测试关联信息。
    /// en-us: Bug title, description, environment, and test linkage data.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前提交人标识。
    /// en-us: Current reporter identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建 Bug。
    /// en-us: Created bug.
    /// </returns>
    Task<SprintBugResult> CreateBugAsync(CreateSprintBugRequest request, string userId);

    /// <summary>
    /// zh-cn: 查询 Bug 列表，可按项目或需求筛选。
    /// en-us: Lists bugs with optional project and requirement filters.
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
    /// zh-cn: Bug 列表。
    /// en-us: Bug list.
    /// </returns>
    Task<IReadOnlyList<SprintBugResult>> ListBugsAsync(string? projectId, string? requirementId);

    /// <summary>
    /// zh-cn: 开发领取 Bug 并生成修复租约。
    /// en-us: Claims a bug for fixing and creates a fix lease.
    /// </summary>
    /// <param name="id">
    /// zh-cn: Bug 标识。
    /// en-us: Bug identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 可选设备标识。
    /// en-us: Optional device identifier.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前开发用户标识。
    /// en-us: Current developer identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 新建租约。
    /// en-us: Created lease.
    /// </returns>
    Task<SprintTaskLeaseResult> ClaimBugAsync(string id, ClaimSprintTaskRequest request, string userId);

    /// <summary>
    /// zh-cn: 标记 Bug 已修复并关闭对应活跃租约。
    /// en-us: Marks a bug as fixed and completes its active lease.
    /// </summary>
    /// <param name="id">
    /// zh-cn: Bug 标识。
    /// en-us: Bug identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的 Bug。
    /// en-us: Updated bug.
    /// </returns>
    Task<SprintBugResult> FixBugAsync(string id);

    /// <summary>
    /// zh-cn: 关闭已修复待回归的 Bug，并在需求没有未关闭缺陷时恢复需求健康状态。
    /// en-us: Closes a fixed bug and restores the requirement health state when no open bugs remain.
    /// </summary>
    /// <param name="id">
    /// zh-cn: Bug 标识。
    /// en-us: Bug identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 已关闭的 Bug。
    /// en-us: Closed bug.
    /// </returns>
    Task<SprintBugResult> CloseBugAsync(string id);

    /// <summary>
    /// zh-cn: 查询活跃任务租约。
    /// en-us: Lists active task leases.
    /// </summary>
    /// <returns>
    /// zh-cn: 活跃租约列表。
    /// en-us: Active lease list.
    /// </returns>
    Task<IReadOnlyList<SprintTaskLeaseResult>> ListActiveLeasesAsync();

    /// <summary>
    /// zh-cn: 生成 MVP 工作台统计信息。
    /// en-us: Builds MVP workbench summary metrics.
    /// </summary>
    /// <returns>
    /// zh-cn: 工作台统计。
    /// en-us: Workbench summary metrics.
    /// </returns>
    Task<SprintMvpSummaryResult> GetSummaryAsync();
}
