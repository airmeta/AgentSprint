using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Service.Impls.AgileServices;

namespace AgentSprint.Tests;

public sealed class ProposalServiceTests
{
    [Fact]
    public async Task CreateAsync_BindsProjectMaterials()
    {
        var service = CreateService();
        var project = await SeedProjectAsync(service.ProjectDomain, service.MemberDomain);
        var material = await SeedMaterialAsync(service.MaterialDomain, project.Id);

        var proposal = await service.Service.CreateAsync(
            project.Id,
            new CreateSprintProposalRequest(
                "升级支付体验",
                [material.Id],
                "基于材料生成提案",
                "提案正文",
                "提案摘要"),
            "pm-1",
            false);

        Assert.Equal(SprintProposalStatuses.Draft, proposal.Status);
        Assert.Equal(SprintProposalSourceTypes.ProjectMaterials, proposal.SourceType);
        var source = Assert.Single(proposal.Materials);
        Assert.Equal(material.Id, source.MaterialId);
        Assert.Equal(material.Sha256, source.MaterialVersionHash);
        Assert.NotNull(source.Material);
    }

    [Fact]
    public async Task CreateAsync_RejectsCrossProjectMaterial()
    {
        var service = CreateService();
        var project = await SeedProjectAsync(service.ProjectDomain, service.MemberDomain);
        var otherProject = await SeedProjectAsync(
            service.ProjectDomain,
            service.MemberDomain,
            code: "OTHER",
            projectManagerId: "pm-2");
        var otherMaterial = await SeedMaterialAsync(service.MaterialDomain, otherProject.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Service.CreateAsync(
                project.Id,
                new CreateSprintProposalRequest("跨项目材料", [otherMaterial.Id]),
                "pm-1",
                false));

        Assert.Equal("Proposal source materials must be active files in the same project.", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_RejectsConfirmedProposal()
    {
        var service = CreateService();
        var project = await SeedProjectAsync(service.ProjectDomain, service.MemberDomain);
        var proposal = await service.Service.CreateAsync(
            project.Id,
            new CreateSprintProposalRequest("待确认提案"),
            "pm-1",
            false);
        await service.Service.ConfirmAsync(proposal.Id, "pm-1", false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Service.UpdateAsync(
                proposal.Id,
                new UpdateSprintProposalRequest("确认后修改"),
                "pm-1",
                false));

        Assert.Equal("Proposal status does not allow editing.", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_CanRebindPreviouslyRemovedMaterial()
    {
        var relationDomain = new InMemorySprintProposalMaterialDomain();
        var service = CreateService(relationDomain);
        var project = await SeedProjectAsync(service.ProjectDomain, service.MemberDomain);
        var material = await SeedMaterialAsync(service.MaterialDomain, project.Id);
        var proposal = await service.Service.CreateAsync(
            project.Id,
            new CreateSprintProposalRequest("材料提案", [material.Id]),
            "pm-1",
            false);

        await service.Service.UpdateAsync(
            proposal.Id,
            new UpdateSprintProposalRequest("移除材料"),
            "pm-1",
            false);
        var removedRelation = Assert.Single(await relationDomain.ListIncludingDeletedAsync());
        Assert.Equal(1, removedRelation.IsDelete);

        var rebound = await service.Service.UpdateAsync(
            proposal.Id,
            new UpdateSprintProposalRequest("重新绑定材料", MaterialIds: [material.Id]),
            "pm-1",
            false);

        var relation = Assert.Single(await relationDomain.ListIncludingDeletedAsync());
        Assert.Equal(0, relation.IsDelete);
        Assert.Equal(material.Id, Assert.Single(rebound.Materials).MaterialId);
    }

    [Fact]
    public async Task ListAsync_RejectsNonProjectMember()
    {
        var service = CreateService();
        var project = await SeedProjectAsync(service.ProjectDomain, service.MemberDomain);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Service.ListAsync(
                project.Id,
                null,
                null,
                null,
                1,
                20,
                "outsider",
                false));

        Assert.Equal("No proposal permission.", exception.Message);
    }

    private static TestServiceBundle CreateService(InMemorySprintProposalMaterialDomain? relationDomain = null)
    {
        var projectDomain = new InMemorySprintProjectDomain();
        var memberDomain = new InMemorySprintProjectMemberDomain();
        var materialDomain = new InMemorySprintProjectMaterialDomain();
        var service = new ProposalService(
            projectDomain,
            memberDomain,
            materialDomain,
            new InMemorySprintProposalDomain(),
            relationDomain ?? new InMemorySprintProposalMaterialDomain(),
            new InMemorySprintProposalConversationDomain(),
            new InMemorySprintProposalRequirementDomain());
        return new TestServiceBundle(service, projectDomain, memberDomain, materialDomain);
    }

    private static async Task<SprintProjectEntity> SeedProjectAsync(
        InMemorySprintProjectDomain projectDomain,
        InMemorySprintProjectMemberDomain memberDomain,
        string code = "PROPOSAL",
        string projectManagerId = "pm-1")
    {
        var project = new SprintProjectEntity
        {
            Code = code,
            Name = $"{code} project",
            ProjectManagerId = projectManagerId,
            CreatedBy = projectManagerId
        };
        await projectDomain.CreateAsync(project);
        await memberDomain.CreateAsync(new SprintProjectMemberEntity
        {
            ProjectId = project.Id,
            UserId = projectManagerId,
            Role = SprintProjectMemberRoles.ProjectManager,
            Status = SprintProjectMemberStatuses.Active
        });
        return project;
    }

    private static async Task<SprintProjectMaterialEntity> SeedMaterialAsync(
        InMemorySprintProjectMaterialDomain materialDomain,
        string projectId)
    {
        var material = new SprintProjectMaterialEntity
        {
            ProjectId = projectId,
            ItemType = SprintProjectMaterialItemTypes.File,
            Name = "prd.md",
            OriginalFileName = "prd.md",
            Extension = ".md",
            ContentType = "text/markdown",
            SizeBytes = 128,
            RelativePath = $"project-materials/{projectId}/files/2026/06/material.md",
            Sha256 = Guid.NewGuid().ToString("N"),
            ExtractStatus = SprintProjectMaterialExtractStatuses.Completed,
            ExtractedTextPath = $"project-materials/{projectId}/extracted/material.txt",
            UploadedBy = "pm-1"
        };
        await materialDomain.CreateAsync(material);
        return material;
    }

    private sealed record TestServiceBundle(
        ProposalService Service,
        InMemorySprintProjectDomain ProjectDomain,
        InMemorySprintProjectMemberDomain MemberDomain,
        InMemorySprintProjectMaterialDomain MaterialDomain);
}

internal sealed class InMemorySprintProposalDomain :
    InMemoryDomainBase<SprintProposalEntity>,
    ISprintProposalDomain;

internal sealed class InMemorySprintProposalMaterialDomain :
    InMemoryDomainBase<SprintProposalMaterialEntity>,
    ISprintProposalMaterialDomain;

internal sealed class InMemorySprintProposalConversationDomain :
    InMemoryDomainBase<SprintProposalConversationEntity>,
    ISprintProposalConversationDomain;

internal sealed class InMemorySprintProposalRequirementDomain :
    InMemoryDomainBase<SprintProposalRequirementEntity>,
    ISprintProposalRequirementDomain;
