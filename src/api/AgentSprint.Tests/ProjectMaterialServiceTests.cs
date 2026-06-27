using AgentSprint.Model.Modules.Agile;
using AgentSprint.Service.Impls.AgileServices;
using AgentSprint.Model.Modules.Agile.Dtos;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace AgentSprint.Tests;

public sealed class ProjectMaterialServiceTests
{
    [Fact]
    public async Task UploadAsync_StoresFileMetadataAndExtractsText()
    {
        using var temp = new TempDirectory();
        var materialDomain = new InMemorySprintProjectMaterialDomain();
        var eventDomain = new InMemorySprintProjectMaterialEventDomain();
        var service = CreateService(temp.Path, materialDomain: materialDomain, eventDomain: eventDomain);
        var project = await SeedProjectAsync(service.ProjectDomain, service.MemberDomain);

        var result = await service.Service.UploadAsync(
            project.Id,
            null,
            "需求资料",
            ["PRD"],
            "first upload",
            CreateFormFile("prd.md", "# PRD\ncontent"),
            "pm-1",
            false);

        Assert.Equal(SprintProjectMaterialItemTypes.File, result.ItemType);
        Assert.Equal(SprintProjectMaterialExtractStatuses.Completed, result.ExtractStatus);
        Assert.Equal(".md", result.Extension);
        Assert.NotNull(result.RelativePath);
        Assert.True(File.Exists(Path.Combine(temp.Path, result.RelativePath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Contains("PRD", result.Tags);
        Assert.Single(await materialDomain.ListAsync());
        Assert.Single(await eventDomain.ListAsync(entity => entity.EventType == SprintProjectMaterialEventTypes.Uploaded));
    }

    [Fact]
    public async Task UploadAsync_RejectsNonProjectMember()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        var project = await SeedProjectAsync(service.ProjectDomain, service.MemberDomain);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Service.UploadAsync(
                project.Id,
                null,
                null,
                null,
                null,
                CreateFormFile("prd.md", "content"),
                "outsider",
                false));

        Assert.Equal("No project material write permission.", exception.Message);
    }

    [Fact]
    public async Task DeleteAsync_PreventsDownloadAfterSoftDelete()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        var project = await SeedProjectAsync(service.ProjectDomain, service.MemberDomain);
        var material = await service.Service.UploadAsync(
            project.Id,
            null,
            null,
            null,
            null,
            CreateFormFile("prd.txt", "content"),
            "pm-1",
            false);

        Assert.True(await service.Service.DeleteAsync(material.Id, "pm-1", false));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Service.OpenDownloadAsync(material.Id, "pm-1", false));

        Assert.Equal("Project material does not exist.", exception.Message);
    }

    [Fact]
    public async Task MoveAsync_RejectsMovingFolderIntoItsChild()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        var project = await SeedProjectAsync(service.ProjectDomain, service.MemberDomain);
        var parent = await service.Service.CreateFolderAsync(
            project.Id,
            new CreateSprintProjectMaterialFolderRequest("parent"),
            "pm-1",
            false);
        var child = await service.Service.CreateFolderAsync(
            project.Id,
            new CreateSprintProjectMaterialFolderRequest("child", parent.Id),
            "pm-1",
            false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Service.MoveAsync(parent.Id, new MoveSprintProjectMaterialRequest(child.Id), "pm-1", false));

        Assert.Equal("Folder cannot be moved into its child folder.", exception.Message);
    }

    [Fact]
    public async Task OpenDownloadAsync_RejectsEscapedRelativePath()
    {
        using var temp = new TempDirectory();
        var materialDomain = new InMemorySprintProjectMaterialDomain();
        var service = CreateService(temp.Path, materialDomain: materialDomain);
        var project = await SeedProjectAsync(service.ProjectDomain, service.MemberDomain);
        await materialDomain.CreateAsync(new SprintProjectMaterialEntity
        {
            ProjectId = project.Id,
            ItemType = SprintProjectMaterialItemTypes.File,
            Name = "escaped.txt",
            OriginalFileName = "escaped.txt",
            ContentType = "text/plain",
            RelativePath = "../escaped.txt",
            UploadedBy = "pm-1",
            ExtractStatus = SprintProjectMaterialExtractStatuses.Completed
        });
        var material = (await materialDomain.ListAsync()).Single();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Service.OpenDownloadAsync(material.Id, "pm-1", false));

        Assert.Equal("Material path is invalid.", exception.Message);
    }

    private static async Task<SprintProjectEntity> SeedProjectAsync(
        InMemorySprintProjectDomain projectDomain,
        InMemorySprintProjectMemberDomain memberDomain)
    {
        var project = new SprintProjectEntity
        {
            Code = "MAT",
            Name = "Material project",
            CreatedBy = "pm-1"
        };
        await projectDomain.CreateAsync(project);
        await memberDomain.CreateAsync(new SprintProjectMemberEntity
        {
            ProjectId = project.Id,
            UserId = "pm-1",
            Role = SprintProjectMemberRoles.ProjectManager,
            Status = SprintProjectMemberStatuses.Active
        });
        await memberDomain.CreateAsync(new SprintProjectMemberEntity
        {
            ProjectId = project.Id,
            UserId = "dev-1",
            Role = SprintProjectMemberRoles.Developer,
            Status = SprintProjectMemberStatuses.Active
        });
        return project;
    }

    private static TestServiceBundle CreateService(
        string rootPath,
        InMemorySprintProjectMaterialDomain? materialDomain = null,
        InMemorySprintProjectMaterialEventDomain? eventDomain = null)
    {
        var projectDomain = new InMemorySprintProjectDomain();
        var memberDomain = new InMemorySprintProjectMemberDomain();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProjectMaterials:RelativeRootPath"] = "project-materials",
                ["ProjectMaterials:MaxFileSizeMb"] = "1",
                ["ProjectMaterials:AllowedExtensions:0"] = ".md",
                ["ProjectMaterials:AllowedExtensions:1"] = ".txt"
            })
            .Build();
        var service = new ProjectMaterialService(
            projectDomain,
            memberDomain,
            materialDomain ?? new InMemorySprintProjectMaterialDomain(),
            eventDomain ?? new InMemorySprintProjectMaterialEventDomain(),
            configuration,
            new TestHostEnvironment(rootPath));
        return new TestServiceBundle(service, projectDomain, memberDomain);
    }

    private static IFormFile CreateFormFile(string fileName, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
    }

    private sealed record TestServiceBundle(
        ProjectMaterialService Service,
        InMemorySprintProjectDomain ProjectDomain,
        InMemorySprintProjectMemberDomain MemberDomain);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }

        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "AgentSprint.Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"agentsprint-materials-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
