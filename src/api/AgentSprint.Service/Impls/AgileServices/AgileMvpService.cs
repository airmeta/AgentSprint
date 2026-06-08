using System.Text;

using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Model.Modules.Tests;
using AgentSprint.Model.Modules.Tests.Domains;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.AgileServices;
using AgentSprint.Service.Services.SecurityServices;

namespace AgentSprint.Service.Impls.AgileServices;

public sealed class AgileMvpService : AgentSprintServiceBase, IAgileMvpService
{
    private const string DefaultWorkspacePath = @"F:\AI\AgentSprint";
    private const string DefaultMcpEndpoint = "http://localhost:5010/mcp";
    private const string SyncTestEnvironmentOnCompletionKey = "Sprint:Requirement:SyncTestEnvironmentOnCompletion";

    private readonly ISprintProjectDomain _projectDomain;
    private readonly ISprintProjectMemberDomain _projectMemberDomain;
    private readonly ISprintProjectEndpointDomain _endpointDomain;
    private readonly ISprintFeatureModuleDomain _moduleDomain;
    private readonly ISprintRequirementDomain _requirementDomain;
    private readonly ISprintSkillDomain _skillDomain;
    private readonly ISprintFeatureSuggestionDomain _suggestionDomain;
    private readonly ISprintRequirementFeedbackDomain _feedbackDomain;
    private readonly ISprintRequirementReviewDomain _reviewDomain;
    private readonly ISprintDevelopmentTaskDomain _taskDomain;
    private readonly ISprintBugDomain _bugDomain;
    private readonly ISprintTaskLeaseDomain _leaseDomain;
    private readonly IRuntimeEnvironmentDomain _runtimeEnvironmentDomain;
    private readonly ITestPlanDomain _testPlanDomain;
    private readonly IRequirementDecompositionService _decompositionService;
    private readonly ISystemConfigurationService _configurationService;

    /// <summary>
    /// zh-cn: 创建敏捷 MVP 服务，注入项目、需求、评审、开发任务、缺陷和租约领域对象以维护最小交付闭环。
    /// en-us: Creates the agile MVP service with project, requirement, review, development-task, bug, and lease domains to maintain the minimal delivery loop.
    /// </summary>
    /// <param name="projectDomain">
    /// zh-cn: 项目领域对象。
    /// en-us: Project domain object.
    /// </param>
    /// <param name="requirementDomain">
    /// zh-cn: 需求领域对象。
    /// en-us: Requirement domain object.
    /// </param>
    /// <param name="reviewDomain">
    /// zh-cn: 需求评审领域对象。
    /// en-us: Requirement-review domain object.
    /// </param>
    /// <param name="taskDomain">
    /// zh-cn: 开发任务领域对象。
    /// en-us: Development-task domain object.
    /// </param>
    /// <param name="bugDomain">
    /// zh-cn: 缺陷领域对象。
    /// en-us: Bug domain object.
    /// </param>
    /// <param name="leaseDomain">
    /// zh-cn: 任务租约领域对象。
    /// en-us: Task-lease domain object.
    /// </param>
    /// <param name="decompositionService">
    /// zh-cn: 需求拆解服务，默认使用本地规则生成任务，可替换为 AI/MCP 工具实现。
    /// en-us: Requirement decomposition service; the default uses local rules and can be replaced by an AI/MCP tool implementation.
    /// </param>
    public AgileMvpService(
        ISprintProjectDomain projectDomain,
        ISprintProjectMemberDomain projectMemberDomain,
        ISprintProjectEndpointDomain endpointDomain,
        ISprintFeatureModuleDomain moduleDomain,
        ISprintRequirementDomain requirementDomain,
        ISprintSkillDomain skillDomain,
        ISprintFeatureSuggestionDomain suggestionDomain,
        ISprintRequirementFeedbackDomain feedbackDomain,
        ISprintRequirementReviewDomain reviewDomain,
        ISprintDevelopmentTaskDomain taskDomain,
        ISprintBugDomain bugDomain,
        ISprintTaskLeaseDomain leaseDomain,
        IRuntimeEnvironmentDomain runtimeEnvironmentDomain,
        ITestPlanDomain testPlanDomain,
        IRequirementDecompositionService decompositionService,
        ISystemConfigurationService configurationService)
    {
        _projectDomain = projectDomain;
        _projectMemberDomain = projectMemberDomain;
        _endpointDomain = endpointDomain;
        _moduleDomain = moduleDomain;
        _requirementDomain = requirementDomain;
        _skillDomain = skillDomain;
        _suggestionDomain = suggestionDomain;
        _feedbackDomain = feedbackDomain;
        _reviewDomain = reviewDomain;
        _taskDomain = taskDomain;
        _bugDomain = bugDomain;
        _leaseDomain = leaseDomain;
        _runtimeEnvironmentDomain = runtimeEnvironmentDomain;
        _testPlanDomain = testPlanDomain;
        _decompositionService = decompositionService;
        _configurationService = configurationService;
    }

    /// <inheritdoc />
    public async Task<SprintProjectResult> CreateProjectAsync(CreateSprintProjectRequest request, string userId)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Project code and name are required.");
        }

        var projectProfile = NormalizeProjectProfile(
            request.RepositoryUrl,
            request.Description,
            request.FrontendTechStack,
            request.BackendTechStack,
            request.ProjectManagerId,
            request.ProductManagerIds,
            request.DeveloperIds,
            request.TesterIds,
            request.ArchitectId);
        var code = request.Code.Trim();
        var duplicated = await _projectDomain.ListAsync(entity => entity.Code == code);
        if (duplicated.Count > 0)
        {
            throw new InvalidOperationException("Project code already exists.");
        }

        var entity = new SprintProjectEntity
        {
            Code = code,
            Name = request.Name.Trim(),
            RepositoryUrl = projectProfile.RepositoryUrl,
            TestEnvironmentId = NormalizeOptional(request.TestEnvironmentId),
            TestEnvironmentUrl = await ResolveProjectTestEnvironmentUrlAsync(
                request.TestEnvironmentId,
                request.TestEnvironmentUrl),
            Description = projectProfile.Description,
            FrontendTechStack = projectProfile.FrontendTechStack,
            BackendTechStack = projectProfile.BackendTechStack,
            ProjectManagerId = projectProfile.ProjectManagerId,
            ProductManagerIds = SerializeIds(projectProfile.ProductManagerIds),
            DeveloperIds = SerializeIds(projectProfile.DeveloperIds),
            TesterIds = SerializeIds(projectProfile.TesterIds),
            ArchitectId = projectProfile.ArchitectId,
            CreatedBy = userId
        };

        await _projectDomain.CreateAsync(entity);
        await SyncProjectProfileMembersAsync(entity.Id, projectProfile);
        await EnsureDefaultEndpointAndModuleAsync(entity.Id, userId);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintProjectResult>> ListProjectsAsync()
    {
        var entities = await _projectDomain.ListAsync();
        return entities
            .OrderByDescending(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SprintProjectResult> UpdateProjectAsync(string id, UpdateSprintProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Project name is required.");
        }

        var projectProfile = NormalizeProjectProfile(
            request.RepositoryUrl,
            request.Description,
            request.FrontendTechStack,
            request.BackendTechStack,
            request.ProjectManagerId,
            request.ProductManagerIds,
            request.DeveloperIds,
            request.TesterIds,
            request.ArchitectId);
        var entity = await GetProjectOrThrowAsync(id);
        entity.Name = request.Name.Trim();
        entity.RepositoryUrl = projectProfile.RepositoryUrl;
        entity.TestEnvironmentId = NormalizeOptional(request.TestEnvironmentId);
        entity.TestEnvironmentUrl = await ResolveProjectTestEnvironmentUrlAsync(
            request.TestEnvironmentId,
            request.TestEnvironmentUrl);
        entity.Description = projectProfile.Description;
        entity.FrontendTechStack = projectProfile.FrontendTechStack;
        entity.BackendTechStack = projectProfile.BackendTechStack;
        entity.ProjectManagerId = projectProfile.ProjectManagerId;
        entity.ProductManagerIds = SerializeIds(projectProfile.ProductManagerIds);
        entity.DeveloperIds = SerializeIds(projectProfile.DeveloperIds);
        entity.TesterIds = SerializeIds(projectProfile.TesterIds);
        entity.ArchitectId = projectProfile.ArchitectId;
        await _projectDomain.UpdateAsync(entity);
        await SyncProjectProfileMembersAsync(entity.Id, projectProfile);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<SprintSkillResult> CreateSkillAsync(CreateSprintSkillRequest request, string userId)
    {
        if (string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Content))
        {
            throw new InvalidOperationException("Skill code, name and content are required.");
        }

        var code = request.Code.Trim();
        var duplicated = await _skillDomain.ListAsync(entity => entity.Code == code);
        if (duplicated.Count > 0)
        {
            throw new InvalidOperationException("Skill code already exists.");
        }

        var entity = new SprintSkillEntity
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            Content = request.Content.Trim(),
            CreatedBy = userId
        };
        await _skillDomain.CreateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintSkillResult>> ListSkillsAsync(bool activeOnly = false)
    {
        var entities = await _skillDomain.ListAsync(entity =>
            !activeOnly || entity.Status == SprintSkillStatuses.Active);
        return entities
            .OrderBy(entity => entity.Code)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SprintSkillResult> UpdateSkillAsync(string id, UpdateSprintSkillRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Content))
        {
            throw new InvalidOperationException("Skill name and content are required.");
        }

        var entity = await GetSkillOrThrowAsync(id);
        entity.Name = request.Name.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.Content = request.Content.Trim();
        entity.Status = NormalizeSkillStatus(request.Status ?? entity.Status);
        await _skillDomain.UpdateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<SprintProjectEndpointResult> CreateProjectEndpointAsync(
        CreateSprintProjectEndpointRequest request,
        string userId)
    {
        await GetProjectOrThrowAsync(request.ProjectId);
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Endpoint code and name are required.");
        }

        var code = request.Code.Trim();
        var duplicated = await _endpointDomain.ListAsync(entity =>
            entity.ProjectId == request.ProjectId && entity.Code == code);
        if (duplicated.Count > 0)
        {
            throw new InvalidOperationException("Endpoint code already exists in this project.");
        }

        var entity = new SprintProjectEndpointEntity
        {
            ProjectId = request.ProjectId,
            Code = code,
            Name = request.Name.Trim(),
            Type = NormalizeEndpointType(request.Type),
            OwnerId = NormalizeOptional(request.OwnerId),
            DeveloperIds = SerializeIds(NormalizeIds(request.DeveloperIds)),
            TesterIds = SerializeIds(NormalizeIds(request.TesterIds)),
            SkillIds = SerializeIds(await NormalizeActiveSkillIdsAsync(request.SkillIds)),
            Sort = request.Sort ?? 0,
            CreatedBy = userId
        };
        await _endpointDomain.CreateAsync(entity);
        await SyncEndpointMembersAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintProjectEndpointResult>> ListProjectEndpointsAsync(string? projectId)
    {
        var entities = await _endpointDomain.ListAsync(entity =>
            string.IsNullOrWhiteSpace(projectId) || entity.ProjectId == projectId);
        return entities
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SprintProjectEndpointResult> UpdateProjectEndpointAsync(
        string id,
        UpdateSprintProjectEndpointRequest request)
    {
        var entity = await GetEndpointOrThrowAsync(id);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Endpoint name is required.");
        }

        entity.Name = request.Name.Trim();
        entity.Type = NormalizeEndpointType(request.Type);
        entity.OwnerId = NormalizeOptional(request.OwnerId);
        entity.DeveloperIds = SerializeIds(NormalizeIds(request.DeveloperIds));
        entity.TesterIds = SerializeIds(NormalizeIds(request.TesterIds));
        entity.SkillIds = request.SkillIds is null
            ? entity.SkillIds
            : SerializeIds(await NormalizeActiveSkillIdsAsync(request.SkillIds));
        entity.Sort = request.Sort ?? entity.Sort;
        entity.Status = NormalizeOptional(request.Status) ?? entity.Status;
        await _endpointDomain.UpdateAsync(entity);
        await SyncEndpointMembersAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<SprintFeatureModuleResult> CreateFeatureModuleAsync(
        CreateSprintFeatureModuleRequest request,
        string userId)
    {
        await EnsureEndpointBelongsToProjectAsync(request.EndpointId, request.ProjectId);
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Module code and name are required.");
        }

        var code = request.Code.Trim();
        var duplicated = await _moduleDomain.ListAsync(entity =>
            entity.ProjectId == request.ProjectId &&
            entity.EndpointId == request.EndpointId &&
            entity.Code == code);
        if (duplicated.Count > 0)
        {
            throw new InvalidOperationException("Module code already exists in this endpoint.");
        }

        var entity = new SprintFeatureModuleEntity
        {
            ProjectId = request.ProjectId,
            EndpointId = request.EndpointId,
            Code = code,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            OwnerId = NormalizeOptional(request.OwnerId),
            DeveloperIds = SerializeIds(NormalizeIds(request.DeveloperIds)),
            TesterIds = SerializeIds(NormalizeIds(request.TesterIds)),
            Sort = request.Sort ?? 0,
            CreatedBy = userId
        };
        await _moduleDomain.CreateAsync(entity);
        await SyncModuleMembersAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintFeatureModuleResult>> ListFeatureModulesAsync(
        string? projectId,
        string? endpointId)
    {
        var entities = await _moduleDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(projectId) || entity.ProjectId == projectId) &&
            (string.IsNullOrWhiteSpace(endpointId) || entity.EndpointId == endpointId));
        return entities
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SprintFeatureModuleResult> UpdateFeatureModuleAsync(
        string id,
        UpdateSprintFeatureModuleRequest request)
    {
        var entity = await GetModuleOrThrowAsync(id);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Module name is required.");
        }

        entity.Name = request.Name.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.OwnerId = NormalizeOptional(request.OwnerId);
        entity.DeveloperIds = SerializeIds(NormalizeIds(request.DeveloperIds));
        entity.TesterIds = SerializeIds(NormalizeIds(request.TesterIds));
        entity.Sort = request.Sort ?? entity.Sort;
        entity.Status = NormalizeOptional(request.Status) ?? entity.Status;
        await _moduleDomain.UpdateAsync(entity);
        await SyncModuleMembersAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> CreateRequirementAsync(
        CreateSprintRequirementRequest request,
        string userId)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId) || string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("ProjectId and Title are required.");
        }

        var (endpointId, moduleId) = await ResolveRequirementOwnershipAsync(
            request.ProjectId,
            request.EndpointId,
            request.ModuleId,
            userId);
        var endpoint = await GetEndpointOrThrowAsync(endpointId);
        var requestedSkillIds = request.SkillIds ?? DeserializeIds(endpoint.SkillIds);
        var skillIds = await NormalizeActiveSkillIdsAsync(requestedSkillIds);

        var entity = new SprintRequirementEntity
        {
            ProjectId = request.ProjectId,
            EndpointId = endpointId,
            ModuleId = moduleId,
            Title = request.Title.Trim(),
            Description = NormalizeOptional(request.Description),
            Priority = Math.Clamp(request.Priority ?? 3, 1, 5),
            CreatedBy = userId,
            Stakeholders = NormalizeOptional(request.Stakeholders),
            SourceRequirementId = NormalizeOptional(request.SourceRequirementId),
            SourceFeedbackId = NormalizeOptional(request.SourceFeedbackId),
            SkillIds = SerializeIds(skillIds)
        };

        await _requirementDomain.CreateAsync(entity);
        await EnsureProjectMemberAsync(entity.ProjectId, userId, SprintProjectMemberRoles.Product);
        return await ToRequirementResultAsync(entity);
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> UpdateRequirementAsync(
        string id,
        UpdateSprintRequirementRequest request,
        string userId)
    {
        var entity = await GetRequirementOrThrowAsync(id);
        EnsureRequirementStatus(entity, SprintRequirementStatuses.Draft);
        EnsureRequirementCreator(entity, userId, "Only requirement creator can update requirement content.");

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("Title is required.");
        }

        entity.Title = request.Title.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.Priority = Math.Clamp(request.Priority ?? entity.Priority, 1, 5);
        entity.Stakeholders = NormalizeOptional(request.Stakeholders);
        var skillIds = request.SkillIds is null
            ? DeserializeIds(entity.SkillIds)
            : await NormalizeActiveSkillIdsAsync(request.SkillIds);
        entity.SkillIds = SerializeIds(skillIds);
        await _requirementDomain.UpdateAsync(entity);
        return await ToRequirementResultAsync(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintRequirementResult>> ListRequirementsAsync(string? projectId)
    {
        var entities = await _requirementDomain.ListAsync(entity =>
            string.IsNullOrWhiteSpace(projectId) || entity.ProjectId == projectId);
        var results = new List<SprintRequirementResult>();

        foreach (var entity in entities
            .OrderBy(entity => entity.Priority)
            .ThenByDescending(entity => entity.CreateTime))
        {
            results.Add(await ToRequirementResultAsync(entity));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<SprintRequirementFeedbackResult> CreateRequirementFeedbackAsync(
        string requirementId,
        CreateSprintRequirementFeedbackRequest request,
        string userId)
    {
        var requirement = await GetRequirementOrThrowAsync(requirementId);
        EnsureRequirementStatus(
            requirement,
            SprintRequirementStatuses.Tested,
            SprintRequirementStatuses.Completed);
        if (!string.IsNullOrWhiteSpace(requirement.SourceFeedbackId))
        {
            throw new InvalidOperationException("Follow-up requirement converted from feedback cannot create feedback again.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("Feedback title is required.");
        }

        var developmentTaskId = NormalizeOptional(request.DevelopmentTaskId);
        if (!string.IsNullOrWhiteSpace(developmentTaskId))
        {
            var task = await GetDevelopmentTaskOrThrowAsync(developmentTaskId);
            if (!string.Equals(task.RequirementId, requirement.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Feedback task does not belong to this requirement.");
            }

            if (task.Status != SprintDevelopmentTaskStatuses.Completed)
            {
                throw new InvalidOperationException("Only completed tasks can receive feedback.");
            }
        }

        var feedback = new SprintRequirementFeedbackEntity
        {
            ProjectId = requirement.ProjectId,
            RequirementId = requirement.Id,
            DevelopmentTaskId = developmentTaskId,
            Title = request.Title.Trim(),
            Content = NormalizeOptional(request.Content),
            CreatedBy = userId
        };

        await _feedbackDomain.CreateAsync(feedback);
        await EnsureProjectMemberAsync(requirement.ProjectId, userId, SprintProjectMemberRoles.Product);
        return ToResult(feedback);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintRequirementFeedbackResult>> ListRequirementFeedbackAsync(string requirementId)
    {
        await GetRequirementOrThrowAsync(requirementId);
        var feedback = await _feedbackDomain.ListAsync(entity => entity.RequirementId == requirementId);
        return feedback
            .OrderByDescending(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> ConvertRequirementFeedbackAsync(
        string requirementId,
        string feedbackId,
        ConvertSprintRequirementFeedbackRequest request,
        string userId)
    {
        return await ConvertRequirementSourcesAsync(
            requirementId,
            new ConvertSprintRequirementSourcesRequest(
                request.Title,
                request.Description,
                request.Priority,
                request.Stakeholders,
                [feedbackId],
                null,
                request.Remark),
            userId);
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> ConvertRequirementSourcesAsync(
        string requirementId,
        ConvertSprintRequirementSourcesRequest request,
        string userId)
    {
        var sourceRequirement = await GetRequirementOrThrowAsync(requirementId);
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("Follow-up requirement title is required.");
        }

        var feedback = new List<SprintRequirementFeedbackEntity>();
        foreach (var feedbackId in NormalizeIds(request.FeedbackIds))
        {
            var entity = await GetRequirementFeedbackOrThrowAsync(feedbackId);
            if (!string.Equals(entity.RequirementId, sourceRequirement.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Feedback does not belong to this requirement.");
            }

            if (entity.Status != SprintRequirementFeedbackStatuses.Open)
            {
                throw new InvalidOperationException("Feedback status does not allow conversion.");
            }

            feedback.Add(entity);
        }

        var suggestions = new List<SprintFeatureSuggestionEntity>();
        foreach (var suggestionId in NormalizeIds(request.SuggestionIds))
        {
            var entity = await GetFeatureSuggestionOrThrowAsync(suggestionId);
            if (entity.Status != SprintFeatureSuggestionStatuses.Open)
            {
                throw new InvalidOperationException("Suggestion status does not allow conversion.");
            }

            if (!string.Equals(entity.ProjectId, sourceRequirement.ProjectId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Suggestion does not belong to this project.");
            }

            if (!string.IsNullOrWhiteSpace(entity.RequirementId) &&
                !string.Equals(entity.RequirementId, sourceRequirement.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Suggestion does not belong to this requirement.");
            }

            if (!string.IsNullOrWhiteSpace(entity.EndpointId) &&
                !string.Equals(entity.EndpointId, sourceRequirement.EndpointId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Suggestion endpoint does not match this requirement.");
            }

            if (!string.IsNullOrWhiteSpace(entity.ModuleId) &&
                !string.Equals(entity.ModuleId, sourceRequirement.ModuleId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Suggestion module does not match this requirement.");
            }

            suggestions.Add(entity);
        }

        if (feedback.Count == 0 && suggestions.Count == 0)
        {
            throw new InvalidOperationException("At least one feedback or suggestion source is required.");
        }

        var description = BuildConvertedRequirementDescription(
            NormalizeOptional(request.Description) ?? feedback.FirstOrDefault()?.Content ?? suggestions.FirstOrDefault()?.Content,
            request.Remark);
        var followUp = new SprintRequirementEntity
        {
            ProjectId = sourceRequirement.ProjectId,
            EndpointId = sourceRequirement.EndpointId,
            ModuleId = sourceRequirement.ModuleId,
            Title = request.Title.Trim(),
            Description = description,
            Priority = Math.Clamp(request.Priority ?? sourceRequirement.Priority, 1, 5),
            CreatedBy = userId,
            Stakeholders = NormalizeOptional(request.Stakeholders) ?? sourceRequirement.Stakeholders,
            SourceRequirementId = sourceRequirement.Id,
            SourceFeedbackId = feedback.Count == 1 && suggestions.Count == 0 ? feedback[0].Id : null,
            SkillIds = sourceRequirement.SkillIds
        };

        await _requirementDomain.CreateAsync(followUp);

        var convertedAt = DateTime.UtcNow;
        foreach (var entity in feedback)
        {
            entity.Status = SprintRequirementFeedbackStatuses.Converted;
            entity.ConvertedRequirementId = followUp.Id;
            entity.ConvertedAt = convertedAt;
            await _feedbackDomain.UpdateAsync(entity);
        }

        foreach (var entity in suggestions)
        {
            entity.Status = SprintFeatureSuggestionStatuses.Accepted;
            entity.ConvertedRequirementId = followUp.Id;
            entity.ConvertedAt = convertedAt;
            await _suggestionDomain.UpdateAsync(entity);
        }

        await EnsureProjectMemberAsync(sourceRequirement.ProjectId, userId, SprintProjectMemberRoles.Product);
        return await ToRequirementResultAsync(followUp);
    }

    /// <inheritdoc />
    public async Task<SprintFeatureSuggestionResult> CreateFeatureSuggestionAsync(
        CreateSprintFeatureSuggestionRequest request,
        string userId)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId) || string.IsNullOrWhiteSpace(request.Content))
        {
            throw new InvalidOperationException("ProjectId and Content are required.");
        }

        await GetProjectOrThrowAsync(request.ProjectId);
        var endpointId = NormalizeOptional(request.EndpointId);
        var moduleId = NormalizeOptional(request.ModuleId);
        var requirementId = NormalizeOptional(request.RequirementId);
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            if (string.IsNullOrWhiteSpace(endpointId))
            {
                throw new InvalidOperationException("EndpointId is required when ModuleId is provided.");
            }

            await EnsureModuleBelongsToEndpointAsync(moduleId, endpointId, request.ProjectId);
        }
        else if (!string.IsNullOrWhiteSpace(endpointId))
        {
            await EnsureEndpointBelongsToProjectAsync(endpointId, request.ProjectId);
        }

        if (!string.IsNullOrWhiteSpace(requirementId))
        {
            var requirement = await GetRequirementOrThrowAsync(requirementId);
            if (!string.Equals(requirement.ProjectId, request.ProjectId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Suggestion requirement does not belong to this project.");
            }

            if (!string.IsNullOrWhiteSpace(endpointId) &&
                !string.Equals(requirement.EndpointId, endpointId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Suggestion endpoint does not match the requirement.");
            }

            if (!string.IsNullOrWhiteSpace(moduleId) &&
                !string.Equals(requirement.ModuleId, moduleId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Suggestion module does not match the requirement.");
            }
        }

        await EnsureProjectParticipantAsync(request.ProjectId, userId);
        var entity = new SprintFeatureSuggestionEntity
        {
            ProjectId = request.ProjectId,
            EndpointId = endpointId,
            ModuleId = moduleId,
            RequirementId = requirementId,
            Content = request.Content.Trim(),
            CreatedBy = userId
        };
        await _suggestionDomain.CreateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintFeatureSuggestionResult>> ListFeatureSuggestionsAsync(
        string? projectId,
        string? moduleId,
        string? requirementId)
    {
        var entities = await _suggestionDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(projectId) || entity.ProjectId == projectId) &&
            (string.IsNullOrWhiteSpace(moduleId) || entity.ModuleId == moduleId) &&
            (string.IsNullOrWhiteSpace(requirementId) || entity.RequirementId == requirementId));
        return entities
            .OrderByDescending(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> SubmitRequirementReviewAsync(
        string id,
        SubmitSprintRequirementReviewRequest request,
        string userId)
    {
        var entity = await GetRequirementOrThrowAsync(id);
        EnsureRequirementStatus(entity, SprintRequirementStatuses.Draft, SprintRequirementStatuses.Rejected);
        EnsureRequirementCreator(entity, userId, "Only requirement creator can submit requirement review.");

        var reviewerIds = request.ReviewerIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (reviewerIds.Count == 0)
        {
            throw new InvalidOperationException("At least one reviewer is required.");
        }

        var existing = await _reviewDomain.ListAsync(review => review.RequirementId == id);
        foreach (var review in existing)
        {
            await _reviewDomain.DeleteAsync(review.Id);
        }

        foreach (var reviewerId in reviewerIds)
        {
            await _reviewDomain.CreateAsync(new SprintRequirementReviewEntity
            {
                ProjectId = entity.ProjectId,
                RequirementId = entity.Id,
                ReviewerId = reviewerId
            });
            await EnsureProjectMemberAsync(entity.ProjectId, reviewerId, SprintProjectMemberRoles.Reviewer);
        }

        entity.Status = SprintRequirementStatuses.PendingReview;
        entity.SubmittedAt = DateTime.UtcNow;
        await _requirementDomain.UpdateAsync(entity);
        return await ToRequirementResultAsync(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintRequirementReviewItemResult>> ListMyPendingReviewsAsync(string reviewerId)
    {
        var reviews = await _reviewDomain.ListAsync(entity =>
            entity.ReviewerId == reviewerId &&
            entity.Status == SprintRequirementReviewStatuses.Pending);
        var results = new List<SprintRequirementReviewItemResult>();

        foreach (var group in reviews.GroupBy(entity => entity.RequirementId))
        {
            var requirement = await _requirementDomain.GetAsync(group.Key);
            if (requirement is null || requirement.Status != SprintRequirementStatuses.PendingReview)
            {
                continue;
            }

            var project = await _projectDomain.GetAsync(requirement.ProjectId);
            if (project is null)
            {
                continue;
            }

            var allReviews = await _reviewDomain.ListAsync(entity => entity.RequirementId == requirement.Id);
            results.Add(new SprintRequirementReviewItemResult(
                await ToRequirementResultAsync(requirement),
                ToResult(project),
                allReviews.OrderBy(entity => entity.CreateTime).Select(ToResult).ToList()));
        }

        return results.OrderByDescending(item => item.Requirement.SubmittedAt).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintRequirementReviewResult>> ListRequirementReviewsAsync(string requirementId)
    {
        await GetRequirementOrThrowAsync(requirementId);
        var reviews = await _reviewDomain.ListAsync(entity => entity.RequirementId == requirementId);
        return reviews
            .OrderBy(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public Task<SprintRequirementResult> ApproveRequirementReviewAsync(
        string id,
        string userId,
        DecideSprintRequirementReviewRequest request)
    {
        return DecideReviewAsync(id, userId, SprintRequirementReviewStatuses.Approved, request.Comment);
    }

    /// <inheritdoc />
    public Task<SprintRequirementResult> RejectRequirementReviewAsync(
        string id,
        string userId,
        DecideSprintRequirementReviewRequest request)
    {
        return DecideReviewAsync(id, userId, SprintRequirementReviewStatuses.Rejected, request.Comment);
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> ApproveRequirementAsync(string id, string userId)
    {
        var entity = await GetRequirementOrThrowAsync(id);
        EnsureRequirementStatus(entity, SprintRequirementStatuses.Approved);

        entity.Status = SprintRequirementStatuses.ReadyForDevelopment;
        entity.ReviewedBy = userId;
        entity.ApprovedAt ??= DateTime.UtcNow;
        await _requirementDomain.UpdateAsync(entity);
        return await ToRequirementResultAsync(entity);
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> VoidRequirementAsync(string id, string userId)
    {
        var entity = await GetRequirementOrThrowAsync(id);
        EnsureRequirementStatus(entity, SprintRequirementStatuses.Rejected);
        EnsureRequirementCreator(entity, userId, "Only requirement creator can void a rejected requirement.");

        entity.Status = SprintRequirementStatuses.Voided;
        entity.VoidedAt = DateTime.UtcNow;
        await _requirementDomain.UpdateAsync(entity);
        return await ToRequirementResultAsync(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintDevelopmentTaskResult>> DecomposeRequirementAsync(
        string id,
        DecomposeSprintRequirementRequest request,
        string userId)
    {
        var requirement = await GetRequirementOrThrowAsync(id);
        EnsureRequirementStatus(
            requirement,
            SprintRequirementStatuses.Approved,
            SprintRequirementStatuses.ReadyForDevelopment,
            SprintRequirementStatuses.Decomposed);

        await EnsureDevelopmentTasksAsync(requirement, request.Instruction, request.AssignmentMode, userId, request.TaskCount);

        if (requirement.Status != SprintRequirementStatuses.Developing)
        {
            requirement.Status = SprintRequirementStatuses.Decomposed;
        }

        await _requirementDomain.UpdateAsync(requirement);

        return await ListDevelopmentTasksAsync(requirement.ProjectId, requirement.Id, null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintDevelopmentTaskResult>> ListDevelopmentTasksAsync(
        string? projectId,
        string? requirementId,
        string? assigneeId,
        string? status = null)
    {
        var entities = await _taskDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(projectId) || entity.ProjectId == projectId) &&
            (string.IsNullOrWhiteSpace(requirementId) || entity.RequirementId == requirementId) &&
            (string.IsNullOrWhiteSpace(assigneeId) || entity.AssigneeId == assigneeId) &&
            (string.IsNullOrWhiteSpace(status) || entity.Status == status));

        return await ToTaskResultsAsync(entities
            .OrderBy(entity => entity.Priority)
            .ThenByDescending(entity => entity.CreateTime));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintDevelopmentTaskResult>> ListParticipatingDevelopmentTasksAsync(
        string? projectId,
        string? requirementId,
        string? assigneeId,
        string? status,
        bool primaryOnly,
        string userId)
    {
        var memberships = await _projectMemberDomain.ListAsync(entity =>
            entity.UserId == userId &&
            entity.Status == SprintProjectMemberStatuses.Active &&
            (string.IsNullOrWhiteSpace(projectId) || entity.ProjectId == projectId));
        var participatingProjectIds = memberships
            .Select(entity => entity.ProjectId)
            .ToHashSet(StringComparer.Ordinal);

        if (participatingProjectIds.Count == 0)
        {
            return [];
        }

        var entities = await _taskDomain.ListAsync(entity =>
            participatingProjectIds.Contains(entity.ProjectId) &&
            (string.IsNullOrWhiteSpace(requirementId) || entity.RequirementId == requirementId) &&
            (string.IsNullOrWhiteSpace(assigneeId) || entity.AssigneeId == assigneeId) &&
            (string.IsNullOrWhiteSpace(status) || entity.Status == status));

        if (primaryOnly)
        {
            entities = await FilterPrimaryDeveloperTasksAsync(entities, userId);
        }

        return await ToTaskResultsAsync(entities
            .OrderBy(entity => entity.Priority)
            .ThenByDescending(entity => entity.CreateTime));
    }

    /// <inheritdoc />
    public async Task<SprintDevelopmentTaskResult> AssignDevelopmentTaskAsync(
        string id,
        AssignSprintDevelopmentTaskRequest request,
        string assignedBy)
    {
        if (string.IsNullOrWhiteSpace(request.AssigneeId))
        {
            throw new InvalidOperationException("AssigneeId is required.");
        }

        var task = await GetDevelopmentTaskOrThrowAsync(id);
        EnsureDevelopmentTaskStatus(
            task,
            SprintDevelopmentTaskStatuses.PendingAssign,
            SprintDevelopmentTaskStatuses.Assigned);
        if (task.Status == SprintDevelopmentTaskStatuses.Assigned &&
            await HasActiveTargetLeaseAsync(SprintTaskTargetTypes.DevelopmentTask, task.Id))
        {
            throw new InvalidOperationException("Target already has an active lease.");
        }
        task.AssigneeId = request.AssigneeId.Trim();
        task.AssignedBy = string.IsNullOrWhiteSpace(assignedBy) ? null : assignedBy.Trim();
        task.Status = SprintDevelopmentTaskStatuses.Assigned;
        task.AssignedAt = DateTime.UtcNow;
        var taskPrompt = await BuildTaskPromptAsync(task);
        task.Prompt = taskPrompt.Prompt;
        await _taskDomain.UpdateAsync(task);
        await EnsureProjectMemberAsync(task.ProjectId, task.AssigneeId, SprintProjectMemberRoles.Developer);

        var requirement = await GetRequirementOrThrowAsync(task.RequirementId);
        requirement.Status = SprintRequirementStatuses.Developing;
        requirement.DeveloperId = task.AssigneeId;
        await _requirementDomain.UpdateAsync(requirement);

        return await ToTaskResultAsync(task);
    }

    /// <inheritdoc />
    public async Task<SprintTaskLeaseResult> ClaimDevelopmentTaskAsync(
        string id,
        ClaimSprintTaskRequest request,
        string userId)
    {
        var task = await GetDevelopmentTaskOrThrowAsync(id);
        if (!string.Equals(task.AssigneeId, userId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Task is not assigned to current user.");
        }

        EnsureDevelopmentTaskStatus(
            task,
            SprintDevelopmentTaskStatuses.Assigned,
            SprintDevelopmentTaskStatuses.InProgress);

        return await CreateTargetLeaseAsync(
            task.ProjectId,
            SprintTaskTargetTypes.DevelopmentTask,
            task.Id,
            userId,
            request.OwnerDevice);
    }

    /// <inheritdoc />
    public async Task<SprintTaskPromptResult> GetDevelopmentTaskPromptAsync(string id, string userId)
    {
        var task = await GetDevelopmentTaskOrThrowAsync(id);
        if (!string.Equals(task.AssigneeId, userId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Task is not assigned to current user.");
        }

        var taskPrompt = await BuildTaskPromptAsync(task);
        task.Prompt = taskPrompt.Prompt;
        if (task.Status == SprintDevelopmentTaskStatuses.Assigned)
        {
            task.Status = SprintDevelopmentTaskStatuses.InProgress;
            task.StartedAt = DateTime.UtcNow;
        }

        await _taskDomain.UpdateAsync(task);
        return taskPrompt;
    }

    /// <inheritdoc />
    public async Task<SprintDevelopmentTaskResult> CompleteDevelopmentTaskAsync(string id, string userId)
    {
        var task = await GetDevelopmentTaskOrThrowAsync(id);
        if (!string.Equals(task.AssigneeId, userId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Task is not assigned to current user.");
        }

        EnsureDevelopmentTaskStatus(
            task,
            SprintDevelopmentTaskStatuses.Assigned,
            SprintDevelopmentTaskStatuses.InProgress,
            SprintDevelopmentTaskStatuses.Completed);

        task.Status = SprintDevelopmentTaskStatuses.Completed;
        task.CompletedAt ??= DateTime.UtcNow;
        if (task.StartedAt is null)
        {
            task.StartedAt = task.CompletedAt;
        }

        await _taskDomain.UpdateAsync(task);
        await CompleteActiveLeasesAsync(SprintTaskTargetTypes.DevelopmentTask, task.Id);

        var requirementTasks = await _taskDomain.ListAsync(entity => entity.RequirementId == task.RequirementId);
        var currentTask = requirementTasks.SingleOrDefault(entity => entity.Id == task.Id);
        if (currentTask is not null)
        {
            currentTask.Status = SprintDevelopmentTaskStatuses.Completed;
            currentTask.StartedAt = task.StartedAt;
            currentTask.CompletedAt = task.CompletedAt;
        }

        if (requirementTasks.Count > 0 &&
            requirementTasks.All(entity => entity.Status == SprintDevelopmentTaskStatuses.Completed))
        {
            var requirement = await GetRequirementOrThrowAsync(task.RequirementId);
            requirement.Status = SprintRequirementStatuses.ReadyForTest;
            requirement.DevelopmentCompletedAt = DateTime.UtcNow;
            await SyncRequirementTestEnvironmentAsync(requirement, null);
            await _requirementDomain.UpdateAsync(requirement);
        }

        return await ToTaskResultAsync(task);
    }

    /// <inheritdoc />
    public async Task<SprintTaskLeaseResult> ClaimRequirementAsync(
        string id,
        ClaimSprintTaskRequest request,
        string userId)
    {
        var entity = await GetRequirementOrThrowAsync(id);
        EnsureRequirementStatus(
            entity,
            SprintRequirementStatuses.ReadyForDevelopment,
            SprintRequirementStatuses.Decomposed,
            SprintRequirementStatuses.Developing);
        await EnsureNoActiveLeaseAsync(entity.ProjectId, userId);

        entity.Status = SprintRequirementStatuses.Developing;
        entity.DeveloperId = userId;
        await _requirementDomain.UpdateAsync(entity);

        return await CreateTargetLeaseAsync(
            entity.ProjectId,
            SprintTaskTargetTypes.Requirement,
            entity.Id,
            userId,
            request.OwnerDevice);
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> CompleteRequirementDevelopmentAsync(
        string id,
        CompleteSprintDevelopmentRequest request)
    {
        var entity = await GetRequirementOrThrowAsync(id);
        EnsureRequirementStatus(entity, SprintRequirementStatuses.Developing, SprintRequirementStatuses.PendingFix);

        entity.Status = SprintRequirementStatuses.ReadyForTest;
        entity.TestUrl = NormalizeOptional(request.TestUrl);
        entity.DevelopmentCompletedAt = DateTime.UtcNow;
        await SyncRequirementTestEnvironmentAsync(entity, entity.TestUrl);
        await _requirementDomain.UpdateAsync(entity);

        await CompleteActiveLeasesAsync(SprintTaskTargetTypes.Requirement, entity.Id);
        return await ToRequirementResultAsync(entity);
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> StartRequirementTestingAsync(string id)
    {
        var entity = await GetRequirementOrThrowAsync(id);
        EnsureRequirementStatus(entity, SprintRequirementStatuses.ReadyForTest);

        entity.Status = SprintRequirementStatuses.Testing;
        await _requirementDomain.UpdateAsync(entity);
        return await ToRequirementResultAsync(entity);
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> MarkRequirementTestedAsync(string id)
    {
        var entity = await GetRequirementOrThrowAsync(id);
        EnsureRequirementStatus(entity, SprintRequirementStatuses.Testing, SprintRequirementStatuses.ReadyForTest);

        entity.Status = SprintRequirementStatuses.Tested;
        entity.TestedAt = DateTime.UtcNow;
        await _requirementDomain.UpdateAsync(entity);
        return await ToRequirementResultAsync(entity);
    }

    /// <inheritdoc />
    public async Task<SprintRequirementResult> CloseRequirementAsync(string id)
    {
        var entity = await GetRequirementOrThrowAsync(id);
        EnsureRequirementStatus(entity, SprintRequirementStatuses.Tested);

        entity.Status = SprintRequirementStatuses.Completed;
        entity.ClosedAt = DateTime.UtcNow;
        await _requirementDomain.UpdateAsync(entity);
        return await ToRequirementResultAsync(entity);
    }

    /// <inheritdoc />
    public async Task<SprintBugResult> CreateBugAsync(CreateSprintBugRequest request, string userId)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId) ||
            string.IsNullOrWhiteSpace(request.RequirementId) ||
            string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("ProjectId, RequirementId and Title are required.");
        }

        var requirement = await GetRequirementOrThrowAsync(request.RequirementId);
        if (requirement.ProjectId != request.ProjectId)
        {
            throw new InvalidOperationException("Bug project does not match requirement project.");
        }

        var entity = new SprintBugEntity
        {
            ProjectId = request.ProjectId,
            RequirementId = request.RequirementId,
            TestPlanId = NormalizeOptional(request.TestPlanId),
            TestExecutionId = NormalizeOptional(request.TestExecutionId),
            Title = request.Title.Trim(),
            Description = NormalizeOptional(request.Description),
            Environment = string.IsNullOrWhiteSpace(request.Environment) ? "test" : request.Environment.Trim(),
            Severity = NormalizeBugSeverity(request.Severity),
            CreatedBy = userId
        };

        await _bugDomain.CreateAsync(entity);

        requirement.Status = SprintRequirementStatuses.PendingFix;
        await _requirementDomain.UpdateAsync(requirement);

        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintBugResult>> ListBugsAsync(string? projectId, string? requirementId)
    {
        var entities = await _bugDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(projectId) || entity.ProjectId == projectId) &&
            (string.IsNullOrWhiteSpace(requirementId) || entity.RequirementId == requirementId));

        return entities
            .OrderByDescending(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SprintTaskLeaseResult> ClaimBugAsync(
        string id,
        ClaimSprintTaskRequest request,
        string userId)
    {
        var entity = await GetBugOrThrowAsync(id);
        EnsureBugStatus(entity, SprintBugStatuses.Open);
        await EnsureNoActiveLeaseAsync(entity.ProjectId, userId);

        entity.Status = SprintBugStatuses.Fixing;
        entity.DeveloperId = userId;
        await _bugDomain.UpdateAsync(entity);

        var requirement = await GetRequirementOrThrowAsync(entity.RequirementId);
        requirement.Status = SprintRequirementStatuses.PendingFix;
        requirement.DeveloperId = userId;
        await _requirementDomain.UpdateAsync(requirement);

        return await CreateTargetLeaseAsync(
            entity.ProjectId,
            SprintTaskTargetTypes.Bug,
            entity.Id,
            userId,
            request.OwnerDevice);
    }

    /// <inheritdoc />
    public async Task<SprintBugResult> FixBugAsync(string id)
    {
        var entity = await GetBugOrThrowAsync(id);
        EnsureBugStatus(entity, SprintBugStatuses.Fixing);

        entity.Status = SprintBugStatuses.FixedReadyForRegression;
        entity.FixedAt = DateTime.UtcNow;
        await _bugDomain.UpdateAsync(entity);
        await CompleteActiveLeasesAsync(SprintTaskTargetTypes.Bug, entity.Id);

        var requirementBugs = await _bugDomain.ListAsync(bug =>
            bug.RequirementId == entity.RequirementId && bug.Status != SprintBugStatuses.Closed);
        var hasOtherBlockingBugs = requirementBugs.Any(bug => bug.Id != entity.Id);
        if (!hasOtherBlockingBugs)
        {
            var requirement = await GetRequirementOrThrowAsync(entity.RequirementId);
            requirement.Status = SprintRequirementStatuses.ReadyForTest;
            requirement.DevelopmentCompletedAt = DateTime.UtcNow;
            await _requirementDomain.UpdateAsync(requirement);
        }

        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<SprintBugResult> CloseBugAsync(string id)
    {
        var entity = await GetBugOrThrowAsync(id);
        EnsureBugStatus(entity, SprintBugStatuses.FixedReadyForRegression);

        entity.Status = SprintBugStatuses.Closed;
        await _bugDomain.UpdateAsync(entity);

        var requirementBugs = await _bugDomain.ListAsync(bug =>
            bug.RequirementId == entity.RequirementId && bug.Status != SprintBugStatuses.Closed);
        if (requirementBugs.Count == 0)
        {
            var requirement = await GetRequirementOrThrowAsync(entity.RequirementId);
            if (requirement.Status == SprintRequirementStatuses.PendingFix)
            {
                requirement.Status = SprintRequirementStatuses.ReadyForTest;
                await _requirementDomain.UpdateAsync(requirement);
            }
        }

        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SprintTaskLeaseResult>> ListActiveLeasesAsync()
    {
        var now = DateTime.UtcNow;
        var entities = await _leaseDomain.ListAsync(entity =>
            entity.Status == SprintTaskLeaseStatuses.Active && entity.ExpiresAt > now);

        return entities
            .OrderBy(entity => entity.ExpiresAt)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SprintMvpSummaryResult> GetSummaryAsync()
    {
        var projects = await _projectDomain.ListAsync();
        var requirements = await _requirementDomain.ListAsync();
        var bugs = await _bugDomain.ListAsync();
        var leases = await ListActiveLeasesAsync();

        return new SprintMvpSummaryResult(
            projects.Count,
            requirements.Count,
            requirements.Count(entity => entity.Status == SprintRequirementStatuses.ReadyForDevelopment),
            requirements.Count(entity => entity.Status == SprintRequirementStatuses.Developing),
            requirements.Count(entity => entity.Status == SprintRequirementStatuses.ReadyForTest),
            requirements.Count(entity => entity.Status == SprintRequirementStatuses.Testing),
            requirements.Count(entity => entity.Status == SprintRequirementStatuses.Completed),
            bugs.Count(entity => entity.Status is SprintBugStatuses.Open or SprintBugStatuses.Fixing),
            leases.Count);
    }

    private async Task<SprintRequirementResult> DecideReviewAsync(
        string requirementId,
        string userId,
        string decision,
        string? comment)
    {
        var requirement = await GetRequirementOrThrowAsync(requirementId);
        EnsureRequirementStatus(requirement, SprintRequirementStatuses.PendingReview);

        var review = (await _reviewDomain.ListAsync(entity =>
            entity.RequirementId == requirementId && entity.ReviewerId == userId)).SingleOrDefault() ??
            throw new InvalidOperationException("Current user is not a reviewer.");
        EnsureReviewStatus(review, SprintRequirementReviewStatuses.Pending);

        review.Status = decision;
        review.Comment = NormalizeOptional(comment);
        review.ReviewedAt = DateTime.UtcNow;
        await _reviewDomain.UpdateAsync(review);

        var reviews = await _reviewDomain.ListAsync(entity => entity.RequirementId == requirementId);
        var currentReview = reviews.SingleOrDefault(entity => entity.Id == review.Id);
        if (currentReview is not null)
        {
            currentReview.Status = decision;
            currentReview.Comment = review.Comment;
            currentReview.ReviewedAt = review.ReviewedAt;
        }

        if (decision == SprintRequirementReviewStatuses.Rejected)
        {
            requirement.Status = SprintRequirementStatuses.Rejected;
            requirement.ReviewedBy = userId;
        }
        else if (reviews.All(entity => entity.Status == SprintRequirementReviewStatuses.Approved))
        {
            await MarkRequirementApprovedAsync(requirement, reviews);
            await EnsureAutoTestPlansAsync(requirement);
            await _requirementDomain.UpdateAsync(requirement);
            return await ToRequirementResultAsync(requirement);
        }

        await _requirementDomain.UpdateAsync(requirement);
        return await ToRequirementResultAsync(requirement);
    }

    private static Task MarkRequirementApprovedAsync(
        SprintRequirementEntity requirement,
        IEnumerable<SprintRequirementReviewEntity> reviews)
    {
        var reviewList = reviews.ToList();
        requirement.Status = SprintRequirementStatuses.Approved;
        requirement.ReviewedBy = string.Join(",", reviewList.Select(entity => entity.ReviewerId));
        requirement.ApprovedAt ??= DateTime.UtcNow;
        return Task.CompletedTask;
    }

    private async Task EnsureDevelopmentTasksAsync(
        SprintRequirementEntity requirement,
        string? instruction,
        string? assignmentMode,
        string userId,
        int? taskCount)
    {
        var existingTasks = await _taskDomain.ListAsync(entity => entity.RequirementId == requirement.Id);
        if (existingTasks.Count > 0)
        {
            return;
        }

        var tasks = await _decompositionService.DecomposeAsync(requirement, instruction, userId, taskCount);
        var normalizedAssignmentMode = NormalizeAssignmentMode(assignmentMode);
        var developers = await ResolveRequirementDevelopersAsync(requirement);
        var developerIndex = 0;
        foreach (var task in tasks)
        {
            if (normalizedAssignmentMode == SprintTaskAssignmentModes.Auto && developers.Count > 0)
            {
                var assigneeId = developers[developerIndex % developers.Count];
                developerIndex++;
                task.AssigneeId = assigneeId;
                task.AssignedBy = userId;
                task.AssignedAt = DateTime.UtcNow;
                task.Status = SprintDevelopmentTaskStatuses.Assigned;
                task.Prompt = (await BuildTaskPromptAsync(task)).Prompt;
                await EnsureProjectMemberAsync(task.ProjectId, assigneeId, SprintProjectMemberRoles.Developer);
            }
            else
            {
                task.AssigneeId = null;
                task.AssignedBy = null;
                task.AssignedAt = null;
                task.Status = SprintDevelopmentTaskStatuses.PendingAssign;
            }

            await _taskDomain.CreateAsync(task);
        }

        if (normalizedAssignmentMode == SprintTaskAssignmentModes.Auto && developers.Count > 0)
        {
            requirement.Status = SprintRequirementStatuses.Developing;
            requirement.DeveloperId = developers[0];
        }
    }

    private async Task<SprintTaskPromptResult> BuildTaskPromptAsync(SprintDevelopmentTaskEntity task)
    {
        var project = await GetProjectOrThrowAsync(task.ProjectId);
        var workspacePath = SanitizeRepositoryReference(project.RepositoryUrl) ?? DefaultWorkspacePath;
        var mcpEndpoint = await _configurationService.GetValueAsync("Mcp:Endpoint", DefaultMcpEndpoint);
        var mcpSetupPrompt = BuildMcpSetupPromptV2(mcpEndpoint);
        var taskExecutionPrompt = BuildTaskExecutionPrompt(task, project, workspacePath);
        var mergedPrompt = string.Join(
            Environment.NewLine + Environment.NewLine,
            [
                $"# {mcpSetupPrompt.Title}",
                mcpSetupPrompt.Content,
                $"# {taskExecutionPrompt.Title}",
                taskExecutionPrompt.Content
            ]);

        return new SprintTaskPromptResult(task.Id, mergedPrompt, mcpSetupPrompt, taskExecutionPrompt);
    }

    private static SprintTaskPromptSectionResult BuildMcpSetupPromptV2(string mcpEndpoint)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你现在位于 AgentSprint 项目工作区，请只完成 Codex 的 agentsprint MCP 接入配置，不修改项目代码。");
        builder.AppendLine();
        builder.AppendLine("目标：");
        builder.AppendLine("将 agentsprint 远程 HTTP MCP 配置到 Codex，使后续任务可以通过 MCP 自动拉取任务上下文并推进。");
        builder.AppendLine();
        builder.AppendLine("请按下面流程执行：");
        builder.AppendLine("1. 检查当前项目工作区是否为 AgentSprint 项目。");
        builder.AppendLine("2. 检查 `~/.codex/config.toml` 中是否已有 `[mcp_servers.agentsprint]`。");
        builder.AppendLine("3. 如果不存在则新增；如果已存在则更新为下面配置。");
        builder.AppendLine("4. 保留现有其他 MCP 配置，不要覆盖 `node_repl` 等已有配置。");
        builder.AppendLine("5. 默认只配置 MCP endpoint 和 Authorization，不要默认写入 `X-AgentSprint-Api-Base-Url`。");
        builder.AppendLine("6. 只有在用户明确提供“远程 MCP 服务可访问的 AgentSprint API 地址”时，才写入 `X-AgentSprint-Api-Base-Url`。");
        builder.AppendLine("7. 不要把 `http://localhost:5000` 固定写入 `X-AgentSprint-Api-Base-Url`。因为这里的 localhost 对远程 MCP 服务来说通常表示 MCP 服务所在机器，不一定是当前 Codex 开发机。");
        builder.AppendLine("8. Codex HTTP MCP 请求头必须使用 `http_headers` 字段，不要使用 `[mcp_servers.agentsprint.headers]` 子表。");
        builder.AppendLine("9. 配置完成后，验证 Codex 是否能识别 agentsprint MCP；如果需要新对话或重启 Codex 才能生效，请明确告诉我。");
        builder.AppendLine();
        builder.AppendLine("需要写入的 Codex TOML 配置为：");
        builder.AppendLine();
        builder.AppendLine("```toml");
        builder.AppendLine("[mcp_servers.agentsprint]");
        builder.AppendLine($"url = \"{EscapeTomlString(mcpEndpoint)}\"");
        builder.AppendLine("http_headers = { Authorization = \"Bearer <这里填令牌>\" }");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("可选覆盖配置：");
        builder.AppendLine("仅当用户明确提供远程 MCP 服务可访问的 AgentSprint API 地址时，才追加到 `http_headers`：");
        builder.AppendLine();
        builder.AppendLine("```toml");
        builder.AppendLine("http_headers = {");
        builder.AppendLine("  Authorization = \"Bearer <这里填令牌>\",");
        builder.AppendLine("  \"X-AgentSprint-Api-Base-Url\" = \"<远程 MCP 服务可访问的 AgentSprint API 地址>\"");
        builder.AppendLine("}");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("如果当前 Codex 版本不支持 HTTP MCP 的 `http_headers` 字段，请不要继续猜测配置方式，直接说明阻塞点。");

        return new SprintTaskPromptSectionResult(
            "MCP 接入配置",
            builder.ToString().Trim(),
            [
                "首次接入或 Codex 提示未发现 agentsprint MCP 时使用。",
                "在任务推进弹框中选择一个有效 Agent Token 并确认后，系统会把最终接入提示词写入剪切板。",
                "接入提示词只用于修改 Codex MCP 配置，不用于修改项目代码。"
            ],
            [
                "Agent Token 只在创建时展示一次，请保存到本机安全位置。",
                "不要把 Agent Token、数据库密码、SSH 私钥、账号密码或服务器连接串写入任务提示词正文、Git 仓库或聊天记录。",
                "复制接入配置后，请在基于项目的新对话中发送，再复制任务推进提示词继续自动化。"
            ]);
    }

    private static SprintTaskPromptSectionResult BuildMcpSetupPrompt()
    {
        return BuildMcpSetupPromptV2(DefaultMcpEndpoint);
    }

    private static SprintTaskPromptSectionResult BuildTaskExecutionPrompt(
        SprintDevelopmentTaskEntity task,
        SprintProjectEntity project,
        string workspacePath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你正在推进 AgentSprint 平台任务，请通过 agentsprint MCP 按任务 ID 加载上下文，不要依赖这段静态文本获取需求详情。");
        builder.AppendLine();
        builder.AppendLine("任务标识：");
        builder.AppendLine($"项目编码：{project.Code}");
        builder.AppendLine($"任务 ID：{task.Id}");
        builder.AppendLine($"仓库引用：{workspacePath}");
        builder.AppendLine();
        builder.AppendLine("执行顺序：");
        builder.AppendLine($"1. 调用 agentsprint.register_session，参数包含 project_code = \"{project.Code}\"。");
        builder.AppendLine("2. 调用 agentsprint.get_mcp_tool_guide，参数包含 format = \"full\"，读取工具用途、参数、返回结构和推荐流程。");
        builder.AppendLine($"3. 调用 agentsprint.get_agent_skill_pack，参数包含 project_code = \"{project.Code}\"，获取 Skill、后端规则、前端规则和验证命令。");
        builder.AppendLine($"4. 调用 agentsprint.get_task_prompt，参数包含 task_id = \"{task.Id}\"，加载 task_detail、requirement_detail 和任务提示上下文。");
        builder.AppendLine("5. 按 MCP 返回的任务、需求、Skill 包和验证命令完成实现，不从这段静态提示词解析需求正文。");
        builder.AppendLine($"6. 完成后运行相关后端测试和前端类型检查，并调用 agentsprint.complete_my_task，参数包含 task_id = \"{task.Id}\" 回写任务状态。");
        builder.AppendLine("7. 必须读取 complete_my_task 返回的 next_work；如果 next_work.kind = bug 或 task，继续处理，或调用 agentsprint.claim_next_work 领取。");
        builder.AppendLine("8. 如果 next_work.kind = none，不要把会话视为结束；读取 next_work.polling.next_interval_seconds，等待后再次调用 agentsprint.get_next_work。");
        builder.AppendLine("9. 连续空闲轮询时将 idle_round 加 1 后传回 get_next_work 或 heartbeat；发现工作项后将 idle_round 重置为 0。");
        builder.AppendLine("10. 如果 session.offline_requested = true 或 polling.should_continue = false，调用 agentsprint.close_session 并停止轮询。");
        builder.AppendLine("约束：不要在提示词、代码、日志或 MCP 响应中写入 SSH 私钥、数据库密码等敏感明文。");

        return new SprintTaskPromptSectionResult(
            "任务推进提示词",
            builder.ToString().Trim(),
            [
                "日常推进任务时复制这一段。",
                "Codex 已配置 agentsprint MCP 后，应使用任务 ID 通过 MCP 拉取完整任务和需求上下文。",
                "任务完成且验证通过后，再调用 complete_my_task 并按 next_work 继续处理后续缺陷或任务。"
            ],
            [
                "这段提示词只保留任务 ID 和项目编码，需求详情、任务说明、Skill 包和验证命令以 MCP 返回为准。",
                "没有 MCP 时不要继续推进任务，先完成 MCP 接入配置。",
                "如果任务跨前后端，后端测试和前端类型检查都需要执行。",
                "当 next_work.kind = none 时会话仍可继续轮询，只有 session.offline_requested 或 polling.should_continue 要求停止时才调用 close_session。"
            ]);
    }

    private static string EscapeTomlString(string value)
    {
        return value.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private async Task<SprintTaskLeaseResult> CreateLeaseAsync(
        string projectId,
        string targetType,
        string targetId,
        string userId,
        string? ownerDevice)
    {
        var lease = new SprintTaskLeaseEntity
        {
            ProjectId = projectId,
            TargetType = targetType,
            TargetId = targetId,
            ActiveTargetKey = BuildActiveTargetKey(targetType, targetId),
            OwnerId = userId,
            OwnerDevice = NormalizeOptional(ownerDevice),
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        };

        try
        {
            await _leaseDomain.CreateAsync(lease);
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            var now = DateTime.UtcNow;
            var existing = await _leaseDomain.ListAsync(entity =>
                entity.TargetType == targetType &&
                entity.TargetId == targetId &&
                entity.Status == SprintTaskLeaseStatuses.Active &&
                entity.ExpiresAt > now &&
                entity.OwnerId == userId &&
                entity.OwnerDevice == lease.OwnerDevice);
            if (existing.Count > 0)
            {
                return ToResult(existing[0]);
            }

            throw new InvalidOperationException("Target already has an active lease.", ex);
        }

        return ToResult(lease);
    }

    private async Task<SprintTaskLeaseResult> CreateTargetLeaseAsync(
        string projectId,
        string targetType,
        string targetId,
        string userId,
        string? ownerDevice)
    {
        var now = DateTime.UtcNow;
        var normalizedOwnerDevice = NormalizeOptional(ownerDevice);
        await ReleaseExpiredTargetLeasesAsync(targetType, targetId, now);

        var activeLeases = await _leaseDomain.ListAsync(entity =>
            entity.TargetType == targetType &&
            entity.TargetId == targetId &&
            entity.Status == SprintTaskLeaseStatuses.Active &&
            entity.ExpiresAt > now);
        var currentSessionLease = activeLeases.FirstOrDefault(entity =>
            entity.OwnerId == userId &&
            string.Equals(entity.OwnerDevice, normalizedOwnerDevice, StringComparison.Ordinal));
        if (currentSessionLease is not null)
        {
            return ToResult(currentSessionLease);
        }

        if (activeLeases.Count > 0)
        {
            throw new InvalidOperationException("Target already has an active lease.");
        }

        return await CreateLeaseAsync(projectId, targetType, targetId, userId, normalizedOwnerDevice);
    }

    private async Task<bool> HasActiveTargetLeaseAsync(string targetType, string targetId)
    {
        var now = DateTime.UtcNow;
        var activeLeases = await _leaseDomain.ListAsync(entity =>
            entity.TargetType == targetType &&
            entity.TargetId == targetId &&
            entity.Status == SprintTaskLeaseStatuses.Active &&
            entity.ExpiresAt > now);
        return activeLeases.Count > 0;
    }

    private async Task CompleteActiveLeasesAsync(string targetType, string targetId)
    {
        var leases = await _leaseDomain.ListAsync(entity =>
            entity.TargetType == targetType &&
            entity.TargetId == targetId &&
            entity.Status == SprintTaskLeaseStatuses.Active);

        foreach (var lease in leases)
        {
            lease.Status = SprintTaskLeaseStatuses.Completed;
            lease.CompletedAt = DateTime.UtcNow;
            lease.ActiveTargetKey = null;
            await _leaseDomain.UpdateAsync(lease);
        }
    }

    private async Task ReleaseExpiredTargetLeasesAsync(string targetType, string targetId, DateTime now)
    {
        var leases = await _leaseDomain.ListAsync(entity =>
            entity.TargetType == targetType &&
            entity.TargetId == targetId &&
            entity.Status == SprintTaskLeaseStatuses.Active &&
            entity.ExpiresAt <= now);

        foreach (var lease in leases)
        {
            lease.Status = SprintTaskLeaseStatuses.Released;
            lease.CompletedAt ??= now;
            lease.ActiveTargetKey = null;
            await _leaseDomain.UpdateAsync(lease);
        }
    }

    private static string BuildActiveTargetKey(string targetType, string targetId)
    {
        return $"{targetType}:{targetId}";
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        var current = ex;
        while (current is not null)
        {
            var message = current.Message;
            if (message.Contains("IX_sprint_task_lease_ActiveTargetKey", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("ActiveTargetKey", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private async Task EnsureProjectMemberAsync(string projectId, string userId, string role)
    {
        if (string.IsNullOrWhiteSpace(projectId) ||
            string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        var normalizedUserId = userId.Trim();
        var normalizedRole = role.Trim();
        var existing = await _projectMemberDomain.ListAsync(entity =>
            entity.ProjectId == projectId &&
            entity.UserId == normalizedUserId &&
            entity.Role == normalizedRole);
        if (existing.Count > 0)
        {
            var member = existing[0];
            if (member.Status != SprintProjectMemberStatuses.Active)
            {
                member.Status = SprintProjectMemberStatuses.Active;
                await _projectMemberDomain.UpdateAsync(member);
            }

            return;
        }

        await _projectMemberDomain.CreateAsync(new SprintProjectMemberEntity
        {
            ProjectId = projectId,
            UserId = normalizedUserId,
            Role = normalizedRole
        });
    }

    private async Task EnsureProjectParticipantAsync(string projectId, string userId)
    {
        var memberships = await _projectMemberDomain.ListAsync(entity =>
            entity.ProjectId == projectId &&
            entity.UserId == userId &&
            entity.Status == SprintProjectMemberStatuses.Active);
        if (memberships.Count == 0)
        {
            throw new InvalidOperationException("Only project participants can submit suggestions.");
        }
    }

    private async Task SyncProjectProfileMembersAsync(string projectId, ProjectProfile profile)
    {
        await EnsureProjectMemberAsync(projectId, profile.ProjectManagerId, SprintProjectMemberRoles.ProjectManager);
        await EnsureProjectMemberAsync(projectId, profile.ArchitectId, SprintProjectMemberRoles.Architect);
        foreach (var productManagerId in profile.ProductManagerIds)
        {
            await EnsureProjectMemberAsync(projectId, productManagerId, SprintProjectMemberRoles.Product);
        }

        foreach (var developerId in profile.DeveloperIds)
        {
            await EnsureProjectMemberAsync(projectId, developerId, SprintProjectMemberRoles.Developer);
        }

        foreach (var testerId in profile.TesterIds)
        {
            await EnsureProjectMemberAsync(projectId, testerId, SprintProjectMemberRoles.Tester);
        }
    }

    private async Task SyncEndpointMembersAsync(SprintProjectEndpointEntity endpoint)
    {
        await EnsureProjectMemberAsync(endpoint.ProjectId, endpoint.OwnerId, SprintProjectMemberRoles.Product);
        foreach (var developerId in DeserializeIds(endpoint.DeveloperIds))
        {
            await EnsureProjectMemberAsync(endpoint.ProjectId, developerId, SprintProjectMemberRoles.Developer);
        }

        foreach (var testerId in DeserializeIds(endpoint.TesterIds))
        {
            await EnsureProjectMemberAsync(endpoint.ProjectId, testerId, SprintProjectMemberRoles.Tester);
        }
    }

    private async Task SyncModuleMembersAsync(SprintFeatureModuleEntity module)
    {
        await EnsureProjectMemberAsync(module.ProjectId, module.OwnerId, SprintProjectMemberRoles.Product);
        foreach (var developerId in DeserializeIds(module.DeveloperIds))
        {
            await EnsureProjectMemberAsync(module.ProjectId, developerId, SprintProjectMemberRoles.Developer);
        }

        foreach (var testerId in DeserializeIds(module.TesterIds))
        {
            await EnsureProjectMemberAsync(module.ProjectId, testerId, SprintProjectMemberRoles.Tester);
        }
    }

    private async Task<IReadOnlyList<string>> ResolveRequirementDevelopersAsync(SprintRequirementEntity requirement)
    {
        var module = await _moduleDomain.GetAsync(requirement.ModuleId);
        var moduleDevelopers = DeserializeIds(module?.DeveloperIds);
        if (moduleDevelopers.Count > 0)
        {
            return moduleDevelopers;
        }

        var endpoint = await _endpointDomain.GetAsync(requirement.EndpointId);
        var endpointDevelopers = DeserializeIds(endpoint?.DeveloperIds);
        if (endpointDevelopers.Count > 0)
        {
            return endpointDevelopers;
        }

        var project = await GetProjectOrThrowAsync(requirement.ProjectId);
        return DeserializeIds(project.DeveloperIds);
    }

    private async Task<List<SprintDevelopmentTaskEntity>> FilterPrimaryDeveloperTasksAsync(
        IEnumerable<SprintDevelopmentTaskEntity> tasks,
        string userId)
    {
        var filtered = new List<SprintDevelopmentTaskEntity>();
        foreach (var task in tasks)
        {
            var requirement = await _requirementDomain.GetAsync(task.RequirementId);
            if (requirement is null)
            {
                continue;
            }

            var developers = await ResolveRequirementDevelopersAsync(requirement);
            if (developers.Contains(userId, StringComparer.Ordinal))
            {
                filtered.Add(task);
            }
        }

        return filtered;
    }

    private async Task<IReadOnlyList<string>> ResolveRequirementTestersAsync(SprintRequirementEntity requirement)
    {
        var module = await _moduleDomain.GetAsync(requirement.ModuleId);
        var moduleTesters = DeserializeIds(module?.TesterIds);
        if (moduleTesters.Count > 0)
        {
            return moduleTesters;
        }

        var endpoint = await _endpointDomain.GetAsync(requirement.EndpointId);
        var endpointTesters = DeserializeIds(endpoint?.TesterIds);
        if (endpointTesters.Count > 0)
        {
            return endpointTesters;
        }

        var project = await GetProjectOrThrowAsync(requirement.ProjectId);
        return DeserializeIds(project.TesterIds);
    }

    private async Task EnsureAutoTestPlansAsync(SprintRequirementEntity requirement)
    {
        var existing = await _testPlanDomain.ListAsync(entity => entity.RequirementId == requirement.Id);
        if (existing.Count > 0)
        {
            return;
        }

        var testers = await ResolveRequirementTestersAsync(requirement);
        if (testers.Count == 0)
        {
            return;
        }

        foreach (var testerId in testers)
        {
            var plan = new TestPlanEntity
            {
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                TesterId = testerId,
                Name = $"{requirement.Title} 测试计划",
                Environment = "test",
                CreatedBy = testerId
            };
            await _testPlanDomain.CreateAsync(plan);
            await EnsureProjectMemberAsync(requirement.ProjectId, testerId, SprintProjectMemberRoles.Tester);
        }
    }

    private async Task EnsureNoActiveLeaseAsync(string projectId, string userId)
    {
        var now = DateTime.UtcNow;
        var leases = await _leaseDomain.ListAsync(entity =>
            entity.ProjectId == projectId &&
            entity.OwnerId == userId &&
            entity.Status == SprintTaskLeaseStatuses.Active &&
            entity.ExpiresAt > now);

        if (leases.Count > 0)
        {
            throw new InvalidOperationException("User already has an active lease in this project.");
        }
    }

    private async Task<SprintProjectEntity> GetProjectOrThrowAsync(string id)
    {
        return await _projectDomain.GetAsync(id) ??
            throw new InvalidOperationException("Project does not exist.");
    }

    private async Task<SprintProjectEndpointEntity> GetEndpointOrThrowAsync(string id)
    {
        return await _endpointDomain.GetAsync(id) ??
            throw new InvalidOperationException("Endpoint does not exist.");
    }

    private async Task<SprintFeatureModuleEntity> GetModuleOrThrowAsync(string id)
    {
        return await _moduleDomain.GetAsync(id) ??
            throw new InvalidOperationException("Module does not exist.");
    }

    private async Task<SprintRequirementEntity> GetRequirementOrThrowAsync(string id)
    {
        return await _requirementDomain.GetAsync(id) ??
            throw new InvalidOperationException("Requirement does not exist.");
    }

    private async Task<SprintSkillEntity> GetSkillOrThrowAsync(string id)
    {
        return await _skillDomain.GetAsync(id) ??
            throw new InvalidOperationException("Skill does not exist.");
    }

    private async Task EnsureEndpointBelongsToProjectAsync(string endpointId, string projectId)
    {
        var endpoint = await GetEndpointOrThrowAsync(endpointId);
        if (!string.Equals(endpoint.ProjectId, projectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Endpoint does not belong to this project.");
        }
    }

    private async Task EnsureModuleBelongsToEndpointAsync(string moduleId, string endpointId, string projectId)
    {
        await EnsureEndpointBelongsToProjectAsync(endpointId, projectId);
        var module = await GetModuleOrThrowAsync(moduleId);
        if (!string.Equals(module.ProjectId, projectId, StringComparison.Ordinal) ||
            !string.Equals(module.EndpointId, endpointId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Module does not belong to this endpoint.");
        }
    }

    private async Task<(string EndpointId, string ModuleId)> ResolveRequirementOwnershipAsync(
        string projectId,
        string? endpointId,
        string? moduleId,
        string userId)
    {
        var normalizedEndpointId = NormalizeOptional(endpointId);
        var normalizedModuleId = NormalizeOptional(moduleId);
        if (!string.IsNullOrWhiteSpace(normalizedEndpointId) &&
            !string.IsNullOrWhiteSpace(normalizedModuleId))
        {
            await EnsureModuleBelongsToEndpointAsync(normalizedModuleId, normalizedEndpointId, projectId);
            return (normalizedEndpointId, normalizedModuleId);
        }

        if (!string.IsNullOrWhiteSpace(normalizedEndpointId) ||
            !string.IsNullOrWhiteSpace(normalizedModuleId))
        {
            throw new InvalidOperationException("EndpointId and ModuleId must be provided together.");
        }

        await GetProjectOrThrowAsync(projectId);
        var defaultEndpoint = (await _endpointDomain.ListAsync(entity =>
                entity.ProjectId == projectId && entity.Code == "DEFAULT-WEB"))
            .FirstOrDefault();
        if (defaultEndpoint is null)
        {
            defaultEndpoint = new SprintProjectEndpointEntity
            {
                ProjectId = projectId,
                Code = "DEFAULT-WEB",
                Name = "Web网站",
                Type = SprintProjectEndpointTypes.Web,
                CreatedBy = userId
            };
            await _endpointDomain.CreateAsync(defaultEndpoint);
        }

        var defaultModule = (await _moduleDomain.ListAsync(entity =>
                entity.ProjectId == projectId &&
                entity.EndpointId == defaultEndpoint.Id &&
                entity.Code == "GENERAL"))
            .FirstOrDefault();
        if (defaultModule is null)
        {
            defaultModule = new SprintFeatureModuleEntity
            {
                ProjectId = projectId,
                EndpointId = defaultEndpoint.Id,
                Code = "GENERAL",
                Name = "通用模块",
                CreatedBy = userId
            };
            await _moduleDomain.CreateAsync(defaultModule);
        }

        return (defaultEndpoint.Id, defaultModule.Id);
    }

    private async Task EnsureDefaultEndpointAndModuleAsync(string projectId, string userId)
    {
        var existingEndpoint = (await _endpointDomain.ListAsync(entity =>
                entity.ProjectId == projectId && entity.Code == "DEFAULT-WEB"))
            .FirstOrDefault();
        if (existingEndpoint is null)
        {
            existingEndpoint = new SprintProjectEndpointEntity
            {
                ProjectId = projectId,
                Code = "DEFAULT-WEB",
                Name = "Web网站",
                Type = SprintProjectEndpointTypes.Web,
                CreatedBy = userId
            };
            await _endpointDomain.CreateAsync(existingEndpoint);
        }

        var existingModule = (await _moduleDomain.ListAsync(entity =>
                entity.ProjectId == projectId &&
                entity.EndpointId == existingEndpoint.Id &&
                entity.Code == "GENERAL"))
            .FirstOrDefault();
        if (existingModule is null)
        {
            await _moduleDomain.CreateAsync(new SprintFeatureModuleEntity
            {
                ProjectId = projectId,
                EndpointId = existingEndpoint.Id,
                Code = "GENERAL",
                Name = "通用模块",
                CreatedBy = userId
            });
        }
    }

    private async Task<SprintRequirementFeedbackEntity> GetRequirementFeedbackOrThrowAsync(string id)
    {
        return await _feedbackDomain.GetAsync(id) ??
            throw new InvalidOperationException("Feedback does not exist.");
    }

    private async Task<SprintFeatureSuggestionEntity> GetFeatureSuggestionOrThrowAsync(string id)
    {
        return await _suggestionDomain.GetAsync(id) ??
            throw new InvalidOperationException("Suggestion does not exist.");
    }

    private async Task<RuntimeEnvironmentEntity> GetRuntimeEnvironmentOrThrowAsync(string id)
    {
        return await _runtimeEnvironmentDomain.GetAsync(id) ??
            throw new InvalidOperationException("Runtime environment does not exist.");
    }

    private async Task<SprintDevelopmentTaskEntity> GetDevelopmentTaskOrThrowAsync(string id)
    {
        return await _taskDomain.GetAsync(id) ??
            throw new InvalidOperationException("Task does not exist.");
    }

    private async Task<SprintBugEntity> GetBugOrThrowAsync(string id)
    {
        return await _bugDomain.GetAsync(id) ??
            throw new InvalidOperationException("Bug does not exist.");
    }

    private static void EnsureRequirementStatus(SprintRequirementEntity entity, params string[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(entity.Status, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Requirement status does not allow this operation.");
        }
    }

    private static void EnsureRequirementCreator(
        SprintRequirementEntity entity,
        string userId,
        string message)
    {
        if (!string.Equals(entity.CreatedBy, userId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureReviewStatus(SprintRequirementReviewEntity entity, params string[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(entity.Status, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Review status does not allow this operation.");
        }
    }

    private static void EnsureBugStatus(SprintBugEntity entity, params string[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(entity.Status, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Bug status does not allow this operation.");
        }
    }

    private static void EnsureDevelopmentTaskStatus(
        SprintDevelopmentTaskEntity entity,
        params string[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(entity.Status, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Task status does not allow this operation.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? BuildConvertedRequirementDescription(string? description, string? remark)
    {
        var normalizedDescription = NormalizeOptional(description);
        var normalizedRemark = NormalizeOptional(remark);
        if (normalizedRemark is null)
        {
            return normalizedDescription;
        }

        return string.IsNullOrWhiteSpace(normalizedDescription)
            ? $"追加备注: {normalizedRemark}"
            : $"{normalizedDescription}{Environment.NewLine}{Environment.NewLine}追加备注: {normalizedRemark}";
    }

    private static ProjectProfile NormalizeProjectProfile(
        string? repositoryUrl,
        string? description,
        string? frontendTechStack,
        string? backendTechStack,
        string? projectManagerId,
        IReadOnlyList<string>? productManagerIds,
        IReadOnlyList<string>? developerIds,
        IReadOnlyList<string>? testerIds,
        string? architectId)
    {
        var normalizedRepositoryUrl = NormalizeRepositoryUrl(repositoryUrl);
        var normalizedDescription = NormalizeOptional(description);
        var normalizedFrontendTechStack = NormalizeRequired(frontendTechStack, "Frontend tech stack is required.");
        var normalizedBackendTechStack = NormalizeRequired(backendTechStack, "Backend tech stack is required.");
        var normalizedProjectManagerId = NormalizeRequired(projectManagerId, "Project manager is required.");
        var normalizedProductManagerIds = NormalizeIds(productManagerIds);
        var normalizedDeveloperIds = NormalizeIds(developerIds);
        var normalizedTesterIds = NormalizeIds(testerIds);
        var normalizedArchitectId = NormalizeRequired(architectId, "Architect is required.");

        if (normalizedProductManagerIds.Count == 0)
        {
            throw new InvalidOperationException("At least one product manager is required.");
        }

        if (normalizedDeveloperIds.Count == 0)
        {
            throw new InvalidOperationException("At least one developer is required.");
        }

        if (normalizedTesterIds.Count == 0)
        {
            throw new InvalidOperationException("At least one tester is required.");
        }

        return new ProjectProfile(
            normalizedRepositoryUrl,
            normalizedDescription,
            normalizedFrontendTechStack,
            normalizedBackendTechStack,
            normalizedProjectManagerId,
            normalizedProductManagerIds,
            normalizedDeveloperIds,
            normalizedTesterIds,
            normalizedArchitectId);
    }

    private static string NormalizeEndpointType(string? type)
    {
        var normalized = NormalizeOptional(type) ?? SprintProjectEndpointTypes.Other;
        return normalized switch
        {
            SprintProjectEndpointTypes.Ios or
            SprintProjectEndpointTypes.Android or
            SprintProjectEndpointTypes.Desktop or
            SprintProjectEndpointTypes.Web or
            SprintProjectEndpointTypes.Admin or
            SprintProjectEndpointTypes.Other => normalized,
            _ => SprintProjectEndpointTypes.Other
        };
    }

    private static string NormalizeAssignmentMode(string? assignmentMode)
    {
        var normalized = NormalizeOptional(assignmentMode) ?? SprintTaskAssignmentModes.Auto;
        return normalized switch
        {
            SprintTaskAssignmentModes.Manual => SprintTaskAssignmentModes.Manual,
            _ => SprintTaskAssignmentModes.Auto
        };
    }

    private static string NormalizeSkillStatus(string? status)
    {
        var normalized = NormalizeOptional(status) ?? SprintSkillStatuses.Active;
        return normalized switch
        {
            SprintSkillStatuses.Active or SprintSkillStatuses.Disabled => normalized,
            _ => SprintSkillStatuses.Active
        };
    }

    private async Task<IReadOnlyList<string>> NormalizeActiveSkillIdsAsync(IReadOnlyList<string>? skillIds)
    {
        var normalizedSkillIds = NormalizeIds(skillIds);
        if (normalizedSkillIds.Count == 0)
        {
            return [];
        }

        var skills = await _skillDomain.ListAsync(entity =>
            normalizedSkillIds.Contains(entity.Id) &&
            entity.Status == SprintSkillStatuses.Active);
        var activeSkillIds = skills
            .Select(entity => entity.Id)
            .ToHashSet(StringComparer.Ordinal);
        var missingSkillId = normalizedSkillIds.FirstOrDefault(id => !activeSkillIds.Contains(id));
        if (!string.IsNullOrWhiteSpace(missingSkillId))
        {
            throw new InvalidOperationException("Selected skill does not exist or is disabled.");
        }

        return normalizedSkillIds;
    }

    private static string NormalizeRepositoryUrl(string? repositoryUrl)
    {
        var normalized = NormalizeRequired(repositoryUrl, "Repository URL is required.");
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Repository URL must be an http or https URL.");
        }

        return normalized;
    }

    private static string NormalizeRequired(string? value, string message)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            throw new InvalidOperationException(message);
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeIds(IReadOnlyList<string>? ids)
    {
        return (ids ?? [])
            .Select(NormalizeOptional)
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? SerializeIds(IReadOnlyList<string> ids)
    {
        return ids.Count == 0 ? null : string.Join(",", ids);
    }

    private static IReadOnlyList<string> DeserializeIds(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
    }

    private static string? SanitizeRepositoryReference(string? repositoryUrl)
    {
        var normalized = NormalizeOptional(repositoryUrl);
        if (normalized is null)
        {
            return null;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
        {
            return normalized;
        }

        var builder = new UriBuilder(uri)
        {
            Password = string.Empty,
            UserName = string.Empty
        };

        return builder.Uri.ToString();
    }

    private static string NormalizeBugSeverity(string? severity)
    {
        var normalized = NormalizeOptional(severity) ?? SprintBugSeverities.Major;
        return normalized switch
        {
            SprintBugSeverities.Critical or
            SprintBugSeverities.Major or
            SprintBugSeverities.Minor or
            SprintBugSeverities.Trivial => normalized,
            _ => SprintBugSeverities.Major
        };
    }

    private static SprintProjectResult ToResult(SprintProjectEntity entity)
    {
        return new SprintProjectResult(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.RepositoryUrl,
            entity.TestEnvironmentUrl,
            entity.Description,
            entity.FrontendTechStack,
            entity.BackendTechStack,
            entity.ProjectManagerId,
            DeserializeIds(entity.ProductManagerIds),
            DeserializeIds(entity.DeveloperIds),
            entity.ArchitectId,
            entity.Status,
            entity.CreatedBy,
            entity.CreateTime,
            DeserializeIds(entity.TesterIds),
            entity.TestEnvironmentId);
    }

    private static SprintSkillResult ToResult(SprintSkillEntity entity)
    {
        return new SprintSkillResult(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.Content,
            entity.Status,
            entity.CreatedBy,
            entity.CreateTime);
    }

    private static SprintProjectEndpointResult ToResult(SprintProjectEndpointEntity entity)
    {
        return new SprintProjectEndpointResult(
            entity.Id,
            entity.ProjectId,
            entity.Code,
            entity.Name,
            entity.Type,
            entity.OwnerId,
            DeserializeIds(entity.DeveloperIds),
            DeserializeIds(entity.TesterIds),
            entity.Sort,
            entity.Status,
            entity.CreatedBy,
            entity.CreateTime,
            DeserializeIds(entity.SkillIds));
    }

    private static SprintFeatureModuleResult ToResult(SprintFeatureModuleEntity entity)
    {
        return new SprintFeatureModuleResult(
            entity.Id,
            entity.ProjectId,
            entity.EndpointId,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.OwnerId,
            DeserializeIds(entity.DeveloperIds),
            DeserializeIds(entity.TesterIds),
            entity.Sort,
            entity.Status,
            entity.CreatedBy,
            entity.CreateTime);
    }

    private async Task<SprintRequirementResult> ToRequirementResultAsync(SprintRequirementEntity entity)
    {
        return new SprintRequirementResult(
            entity.Id,
            entity.ProjectId,
            entity.Title,
            entity.Description,
            entity.Status,
            entity.Priority,
            entity.CreatedBy,
            entity.Stakeholders,
            entity.ReviewedBy,
            entity.DeveloperId,
            entity.TestUrl,
            entity.ApprovedAt,
            entity.SubmittedAt,
            entity.DevelopmentCompletedAt,
            entity.TestedAt,
            entity.ClosedAt,
            entity.VoidedAt,
            entity.SourceRequirementId,
            entity.SourceFeedbackId,
            await ResolveRequirementHealthAsync(entity),
            entity.CreateTime,
            entity.EndpointId,
            entity.ModuleId,
            DeserializeIds(entity.SkillIds));
    }

    private static SprintFeatureSuggestionResult ToResult(SprintFeatureSuggestionEntity entity)
    {
        return new SprintFeatureSuggestionResult(
            entity.Id,
            entity.ProjectId,
            entity.EndpointId,
            entity.ModuleId,
            entity.RequirementId,
            entity.Content,
            entity.Status,
            entity.CreatedBy,
            entity.ConvertedRequirementId,
            entity.ConvertedAt,
            entity.CreateTime);
    }

    private async Task<string> ResolveRequirementHealthAsync(SprintRequirementEntity entity)
    {
        if (entity.Status == SprintRequirementStatuses.Voided)
        {
            return "voided";
        }

        var bugs = await _bugDomain.ListAsync(bug =>
            bug.RequirementId == entity.Id &&
            bug.Status != SprintBugStatuses.Closed);
        if (bugs.Count > 0)
        {
            return "warn";
        }

        if (entity.Status is SprintRequirementStatuses.Completed or
            SprintRequirementStatuses.ReadyForTest or
            SprintRequirementStatuses.Tested)
        {
            return "success";
        }

        return "primary";
    }

    private async Task SyncRequirementTestEnvironmentAsync(
        SprintRequirementEntity requirement,
        string? explicitTestUrl)
    {
        if (!string.IsNullOrWhiteSpace(explicitTestUrl))
        {
            return;
        }

        var enabled = await _configurationService.GetValueAsync(
            SyncTestEnvironmentOnCompletionKey,
            "false");
        if (!bool.TryParse(enabled, out var shouldSync) || !shouldSync)
        {
            return;
        }

        var project = await GetProjectOrThrowAsync(requirement.ProjectId);
        var environmentUrl = ResolveProjectTestEnvironmentUrl(project);
        if (!string.IsNullOrWhiteSpace(environmentUrl))
        {
            requirement.TestUrl = environmentUrl;
        }
    }

    private static string? ResolveProjectTestEnvironmentUrl(SprintProjectEntity project)
    {
        return NormalizeOptional(project.TestEnvironmentUrl);
    }

    private async Task<string?> ResolveProjectTestEnvironmentUrlAsync(
        string? runtimeEnvironmentId,
        string? fallbackUrl)
    {
        var normalizedEnvironmentId = NormalizeOptional(runtimeEnvironmentId);
        if (normalizedEnvironmentId is null)
        {
            return NormalizeOptional(fallbackUrl);
        }

        var environment = await GetRuntimeEnvironmentOrThrowAsync(normalizedEnvironmentId);
        return NormalizeOptional(environment.FrontendUrl) ??
            NormalizeOptional(environment.ApiBaseUrl) ??
            NormalizeOptional(environment.McpEndpoint) ??
            NormalizeOptional(fallbackUrl);
    }

    private static SprintRequirementReviewResult ToResult(SprintRequirementReviewEntity entity)
    {
        return new SprintRequirementReviewResult(
            entity.Id,
            entity.ProjectId,
            entity.RequirementId,
            entity.ReviewerId,
            entity.Status,
            entity.Comment,
            entity.ReviewedAt,
            entity.CreateTime);
    }

    private async ValueTask<SprintDevelopmentTaskResult> ToTaskResultAsync(SprintDevelopmentTaskEntity entity)
    {
        var requirement = await _requirementDomain.GetAsync(entity.RequirementId);
        return new SprintDevelopmentTaskResult(
            entity.Id,
            entity.ProjectId,
            entity.RequirementId,
            requirement?.EndpointId,
            requirement?.ModuleId,
            entity.Title,
            entity.Description,
            entity.Status,
            entity.Priority,
            entity.AssigneeId,
            entity.AssignedBy,
            entity.CreatedBy,
            entity.Prompt,
            entity.AssignedAt,
            entity.StartedAt,
            entity.CompletedAt,
            entity.UpdateTime,
            entity.CreateTime);
    }

    private async Task<IReadOnlyList<SprintDevelopmentTaskResult>> ToTaskResultsAsync(
        IEnumerable<SprintDevelopmentTaskEntity> entities)
    {
        var results = new List<SprintDevelopmentTaskResult>();
        foreach (var entity in entities)
        {
            results.Add(await ToTaskResultAsync(entity));
        }

        return results;
    }

    private static SprintBugResult ToResult(SprintBugEntity entity)
    {
        return new SprintBugResult(
            entity.Id,
            entity.ProjectId,
            entity.RequirementId,
            entity.TestPlanId,
            entity.TestExecutionId,
            entity.Title,
            entity.Description,
            entity.Environment,
            entity.Severity,
            entity.Status,
            entity.CreatedBy,
            entity.DeveloperId,
            entity.FixedAt,
            entity.CreateTime);
    }

    private static SprintRequirementFeedbackResult ToResult(SprintRequirementFeedbackEntity entity)
    {
        return new SprintRequirementFeedbackResult(
            entity.Id,
            entity.ProjectId,
            entity.RequirementId,
            entity.DevelopmentTaskId,
            entity.Title,
            entity.Content,
            entity.Status,
            entity.CreatedBy,
            entity.ConvertedRequirementId,
            entity.ConvertedAt,
            entity.ClosedAt,
            entity.CreateTime);
    }

    private static SprintTaskLeaseResult ToResult(SprintTaskLeaseEntity entity)
    {
        return new SprintTaskLeaseResult(
            entity.Id,
            entity.ProjectId,
            entity.TargetType,
            entity.TargetId,
            entity.OwnerId,
            entity.OwnerDevice,
            entity.LeaseToken,
            entity.Status,
            entity.ExpiresAt,
            entity.CompletedAt,
            entity.CreateTime);
    }

    private sealed record ProjectProfile(
        string RepositoryUrl,
        string? Description,
        string FrontendTechStack,
        string BackendTechStack,
        string ProjectManagerId,
        IReadOnlyList<string> ProductManagerIds,
        IReadOnlyList<string> DeveloperIds,
        IReadOnlyList<string> TesterIds,
        string ArchitectId);
}
