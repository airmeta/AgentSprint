using System.Text.Json;

using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.AgileServices;

namespace AgentSprint.Service.Impls.AgileServices;

public sealed class ProposalService : AgentSprintServiceBase, IProposalService
{
    private readonly ISprintProjectDomain _projectDomain;
    private readonly ISprintProjectMemberDomain _projectMemberDomain;
    private readonly ISprintProjectMaterialDomain _materialDomain;
    private readonly ISprintProposalDomain _proposalDomain;
    private readonly ISprintProposalMaterialDomain _proposalMaterialDomain;
    private readonly ISprintProposalConversationDomain _conversationDomain;
    private readonly ISprintProposalRequirementDomain _proposalRequirementDomain;

    public ProposalService(
        ISprintProjectDomain projectDomain,
        ISprintProjectMemberDomain projectMemberDomain,
        ISprintProjectMaterialDomain materialDomain,
        ISprintProposalDomain proposalDomain,
        ISprintProposalMaterialDomain proposalMaterialDomain,
        ISprintProposalConversationDomain conversationDomain,
        ISprintProposalRequirementDomain proposalRequirementDomain)
    {
        _projectDomain = projectDomain;
        _projectMemberDomain = projectMemberDomain;
        _materialDomain = materialDomain;
        _proposalDomain = proposalDomain;
        _proposalMaterialDomain = proposalMaterialDomain;
        _conversationDomain = conversationDomain;
        _proposalRequirementDomain = proposalRequirementDomain;
    }

    public async Task<SprintProposalListResult> ListAsync(
        string projectId,
        string? status,
        string? createdBy,
        string? keyword,
        int pageIndex,
        int pageSize,
        string userId,
        bool isSuperAdministrator)
    {
        await EnsureProjectReadableAsync(projectId, userId, isSuperAdministrator);
        var proposals = await _proposalDomain.ListAsync(entity => entity.ProjectId == projectId);
        var query = proposals.AsEnumerable();

        var normalizedStatus = NormalizeOptional(status);
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(entity => entity.Status == normalizedStatus);
        }

        var normalizedCreatedBy = NormalizeOptional(createdBy);
        if (!string.IsNullOrWhiteSpace(normalizedCreatedBy))
        {
            query = query.Where(entity => entity.CreatedBy == normalizedCreatedBy);
        }

        var normalizedKeyword = NormalizeOptional(keyword);
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            query = query.Where(entity =>
                entity.Title.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase) ||
                (entity.Summary?.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (entity.Content?.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var safePageIndex = Math.Max(pageIndex, 1);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);
        var ordered = query
            .OrderByDescending(entity => entity.UpdateTime ?? entity.CreateTime)
            .ThenBy(entity => entity.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var total = ordered.Count;
        var pageItems = ordered
            .Skip((safePageIndex - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();
        var materialRelations = await _proposalMaterialDomain.ListAsync(entity =>
            entity.ProjectId == projectId &&
            pageItems.Select(item => item.Id).Contains(entity.ProposalId));
        var materialMap = materialRelations
            .GroupBy(entity => entity.ProposalId)
            .ToDictionary(group => group.Key, group => group.Select(entity => ToMaterialResult(entity)).ToList());
        var items = pageItems
            .Select(entity => ToResult(
                entity,
                materialMap.GetValueOrDefault(entity.Id) ?? new List<SprintProposalMaterialResult>(),
                null,
                null))
            .ToList();

        return new SprintProposalListResult(items, total, safePageIndex, safePageSize);
    }

    public async Task<SprintProposalResult> CreateAsync(
        string projectId,
        CreateSprintProposalRequest request,
        string userId,
        bool isSuperAdministrator)
    {
        await EnsureProjectWritableAsync(projectId, userId, isSuperAdministrator);
        var title = NormalizeTitle(request.Title);
        var sourceMaterials = await ResolveMaterialsAsync(projectId, request.MaterialIds);
        var entity = new SprintProposalEntity
        {
            ProjectId = projectId,
            Title = title,
            Status = SprintProposalStatuses.Draft,
            SourceType = sourceMaterials.Count > 0
                ? SprintProposalSourceTypes.ProjectMaterials
                : SprintProposalSourceTypes.Manual,
            Instruction = NormalizeOptional(request.Instruction),
            Content = NormalizeOptional(request.Content),
            Summary = NormalizeOptional(request.Summary),
            CreatedBy = userId
        };

        await _proposalDomain.CreateAsync(entity);
        await ReplaceMaterialsAsync(entity, sourceMaterials);
        return await BuildResultAsync(entity);
    }

    public async Task<SprintProposalResult> GetAsync(
        string proposalId,
        string userId,
        bool isSuperAdministrator)
    {
        var entity = await GetProposalOrThrowAsync(proposalId);
        await EnsureProjectReadableAsync(entity.ProjectId, userId, isSuperAdministrator);
        return await BuildResultAsync(entity, includeDetail: true);
    }

    public async Task<SprintProposalResult> UpdateAsync(
        string proposalId,
        UpdateSprintProposalRequest request,
        string userId,
        bool isSuperAdministrator)
    {
        var entity = await GetProposalOrThrowAsync(proposalId);
        await EnsureProjectWritableAsync(entity.ProjectId, userId, isSuperAdministrator);
        if (entity.Status is not (SprintProposalStatuses.Draft or SprintProposalStatuses.Generated))
        {
            throw new InvalidOperationException("Proposal status does not allow editing.");
        }

        entity.Title = NormalizeTitle(request.Title);
        entity.Instruction = NormalizeOptional(request.Instruction);
        entity.Content = NormalizeOptional(request.Content);
        entity.Summary = NormalizeOptional(request.Summary);
        var sourceMaterials = await ResolveMaterialsAsync(entity.ProjectId, request.MaterialIds);
        entity.SourceType = sourceMaterials.Count > 0
            ? SprintProposalSourceTypes.ProjectMaterials
            : SprintProposalSourceTypes.Manual;

        await _proposalDomain.UpdateAsync(entity);
        await ReplaceMaterialsAsync(entity, sourceMaterials);
        return await BuildResultAsync(entity);
    }

    public async Task<SprintProposalResult> ConfirmAsync(
        string proposalId,
        string userId,
        bool isSuperAdministrator)
    {
        var entity = await GetProposalOrThrowAsync(proposalId);
        await EnsureProjectWritableAsync(entity.ProjectId, userId, isSuperAdministrator);
        if (entity.Status is not (SprintProposalStatuses.Draft or SprintProposalStatuses.Generated))
        {
            throw new InvalidOperationException("Proposal status does not allow confirmation.");
        }

        entity.Status = SprintProposalStatuses.Confirmed;
        entity.ConfirmedAt = DateTime.UtcNow;
        await _proposalDomain.UpdateAsync(entity);
        return await BuildResultAsync(entity, includeDetail: true);
    }

    public async Task<SprintProposalResult> VoidAsync(
        string proposalId,
        string userId,
        bool isSuperAdministrator)
    {
        var entity = await GetProposalOrThrowAsync(proposalId);
        await EnsureProjectWritableAsync(entity.ProjectId, userId, isSuperAdministrator);
        if (entity.Status == SprintProposalStatuses.Converted)
        {
            throw new InvalidOperationException("Converted proposal cannot be voided.");
        }

        entity.Status = SprintProposalStatuses.Voided;
        entity.VoidedAt = DateTime.UtcNow;
        await _proposalDomain.UpdateAsync(entity);
        return await BuildResultAsync(entity, includeDetail: true);
    }

    private async Task ReplaceMaterialsAsync(
        SprintProposalEntity proposal,
        IReadOnlyList<SprintProjectMaterialEntity> sourceMaterials)
    {
        var existingRelations = await _proposalMaterialDomain.ListIncludingDeletedAsync(entity => entity.ProposalId == proposal.Id);
        var nextMaterialIds = sourceMaterials.Select(entity => entity.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var relation in existingRelations.Where(entity =>
            entity.IsDelete == 0 &&
            !nextMaterialIds.Contains(entity.MaterialId)))
        {
            relation.IsDelete = 1;
            await _proposalMaterialDomain.UpdateAsync(relation);
        }

        var existingMaterialIds = existingRelations
            .Where(entity => entity.IsDelete == 0 && nextMaterialIds.Contains(entity.MaterialId))
            .Select(entity => entity.MaterialId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var material in sourceMaterials.Where(entity => !existingMaterialIds.Contains(entity.Id)))
        {
            var deletedRelation = existingRelations.FirstOrDefault(entity =>
                entity.IsDelete != 0 &&
                entity.MaterialId == material.Id);
            if (deletedRelation is not null)
            {
                deletedRelation.IsDelete = 0;
                deletedRelation.MaterialVersionHash = material.Sha256;
                deletedRelation.ExtractedTextSnapshotPath = material.ExtractedTextPath;
                await _proposalMaterialDomain.UpdateAsync(deletedRelation);
                continue;
            }

            await _proposalMaterialDomain.CreateAsync(new SprintProposalMaterialEntity
            {
                ProposalId = proposal.Id,
                ProjectId = proposal.ProjectId,
                MaterialId = material.Id,
                MaterialVersionHash = material.Sha256,
                ExtractedTextSnapshotPath = material.ExtractedTextPath
            });
        }
    }

    private async Task<IReadOnlyList<SprintProjectMaterialEntity>> ResolveMaterialsAsync(
        string projectId,
        IReadOnlyList<string>? materialIds)
    {
        var normalizedIds = materialIds?
            .Select(NormalizeOptional)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
        if (normalizedIds.Count == 0)
        {
            return [];
        }

        var materials = await _materialDomain.ListAsync(entity => normalizedIds.Contains(entity.Id));
        if (materials.Count != normalizedIds.Count)
        {
            throw new InvalidOperationException("Proposal source material does not exist.");
        }

        if (materials.Any(entity =>
            entity.ProjectId != projectId ||
            entity.DeletedAt is not null ||
            entity.ItemType != SprintProjectMaterialItemTypes.File))
        {
            throw new InvalidOperationException("Proposal source materials must be active files in the same project.");
        }

        return normalizedIds
            .Select(id => materials.Single(entity => entity.Id == id))
            .ToList();
    }

    private async Task<SprintProposalResult> BuildResultAsync(
        SprintProposalEntity entity,
        bool includeDetail = false)
    {
        var relations = await _proposalMaterialDomain.ListAsync(relation => relation.ProposalId == entity.Id);
        var materialIds = relations.Select(relation => relation.MaterialId).ToHashSet(StringComparer.Ordinal);
        var materials = materialIds.Count == 0
            ? []
            : await _materialDomain.ListAsync(material => materialIds.Contains(material.Id));
        var materialMap = materials.ToDictionary(material => material.Id, material => material);
        var materialResults = relations
            .OrderBy(relation => relation.CreateTime)
            .Select(relation => ToMaterialResult(
                relation,
                materialMap.GetValueOrDefault(relation.MaterialId)))
            .ToList();
        var conversationResults = includeDetail
            ? (await _conversationDomain.ListAsync(conversation => conversation.ProposalId == entity.Id))
                .OrderBy(conversation => conversation.CreateTime)
                .Select(ToConversationResult)
                .ToList()
            : null;
        var requirementResults = includeDetail
            ? (await _proposalRequirementDomain.ListAsync(relation => relation.ProposalId == entity.Id))
                .OrderBy(relation => relation.CreateTime)
                .Select(ToRequirementResult)
                .ToList()
            : null;

        return ToResult(entity, materialResults, conversationResults, requirementResults);
    }

    private async Task<SprintProposalEntity> GetProposalOrThrowAsync(string proposalId)
    {
        var entity = await _proposalDomain.GetAsync(proposalId);
        return entity ?? throw new InvalidOperationException("Proposal does not exist.");
    }

    private async Task EnsureProjectReadableAsync(string projectId, string userId, bool isSuperAdministrator)
    {
        await EnsureProjectExistsAsync(projectId);
        if (isSuperAdministrator)
        {
            return;
        }

        var members = await _projectMemberDomain.ListAsync(entity =>
            entity.ProjectId == projectId &&
            entity.UserId == userId &&
            entity.Status == SprintProjectMemberStatuses.Active);
        if (members.Count == 0)
        {
            throw new InvalidOperationException("No proposal permission.");
        }
    }

    private async Task EnsureProjectWritableAsync(string projectId, string userId, bool isSuperAdministrator)
    {
        await EnsureProjectExistsAsync(projectId);
        if (isSuperAdministrator)
        {
            return;
        }

        var members = await _projectMemberDomain.ListAsync(entity =>
            entity.ProjectId == projectId &&
            entity.UserId == userId &&
            entity.Status == SprintProjectMemberStatuses.Active);
        if (!members.Any(entity =>
            entity.Role is SprintProjectMemberRoles.ProjectManager
                or SprintProjectMemberRoles.Product
                or SprintProjectMemberRoles.Architect))
        {
            throw new InvalidOperationException("No proposal write permission.");
        }
    }

    private async Task EnsureProjectExistsAsync(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("Project id is required.");
        }

        var project = await _projectDomain.GetAsync(projectId);
        if (project is null)
        {
            throw new InvalidOperationException("Project does not exist.");
        }
    }

    private static SprintProposalResult ToResult(
        SprintProposalEntity entity,
        IReadOnlyList<SprintProposalMaterialResult> materials,
        IReadOnlyList<SprintProposalConversationResult>? conversations,
        IReadOnlyList<SprintProposalRequirementResult>? requirements)
    {
        return new SprintProposalResult(
            entity.Id,
            entity.ProjectId,
            entity.Title,
            entity.Status,
            entity.SourceType,
            entity.Instruction,
            entity.Content,
            entity.Summary,
            entity.AiPromptSnapshot,
            entity.AiResultSnapshot,
            entity.CreatedBy,
            entity.ConfirmedAt,
            entity.ConvertedAt,
            entity.VoidedAt,
            entity.UpdateTime,
            entity.CreateTime,
            materials,
            conversations,
            requirements);
    }

    private static SprintProposalMaterialResult ToMaterialResult(
        SprintProposalMaterialEntity entity,
        SprintProjectMaterialEntity? material = null)
    {
        return new SprintProposalMaterialResult(
            entity.Id,
            entity.ProposalId,
            entity.ProjectId,
            entity.MaterialId,
            entity.MaterialVersionHash,
            entity.ExtractedTextSnapshotPath,
            entity.CreateTime,
            material is null ? null : ToMaterialResult(material));
    }

    private static SprintProjectMaterialResult ToMaterialResult(SprintProjectMaterialEntity entity)
    {
        return new SprintProjectMaterialResult(
            entity.Id,
            entity.ProjectId,
            entity.ParentId,
            entity.ItemType,
            entity.Name,
            entity.OriginalFileName,
            entity.Extension,
            entity.ContentType,
            entity.SizeBytes,
            entity.StorageRoot,
            entity.RelativePath,
            entity.Sha256,
            entity.Category,
            DeserializeStringList(entity.TagsJson),
            entity.Description,
            entity.ExtractStatus,
            entity.ExtractedTextPath,
            entity.Summary,
            entity.UploadedBy,
            entity.DeletedAt,
            entity.UpdateTime,
            entity.CreateTime);
    }

    private static SprintProposalConversationResult ToConversationResult(SprintProposalConversationEntity entity)
    {
        return new SprintProposalConversationResult(
            entity.Id,
            entity.ProposalId,
            entity.ProjectId,
            entity.Role,
            entity.Content,
            DeserializeStringList(entity.MaterialIdsJson),
            entity.TokenUsageJson,
            entity.CreatedBy,
            entity.CreateTime);
    }

    private static SprintProposalRequirementResult ToRequirementResult(SprintProposalRequirementEntity entity)
    {
        return new SprintProposalRequirementResult(
            entity.Id,
            entity.ProposalId,
            entity.ProjectId,
            entity.RequirementId,
            DeserializeStringList(entity.MaterialIdsJson),
            entity.CreatedBy,
            entity.CreateTime);
    }

    private static string NormalizeTitle(string value)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Proposal title is required.");
        }

        return normalized.Length > 128 ? normalized[..128] : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyList<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
