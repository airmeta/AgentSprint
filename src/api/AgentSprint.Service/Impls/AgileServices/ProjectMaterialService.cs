using System.Security.Cryptography;
using System.Text.Json;

using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AgentSprint.Service.Impls.AgileServices;

public sealed class ProjectMaterialService : AgentSprintServiceBase, IProjectMaterialService
{
    private static readonly HashSet<string> TextExtractExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".txt"
    };

    private static readonly string[] DefaultAllowedExtensions =
    [
        ".md",
        ".txt",
        ".pdf",
        ".docx",
        ".xlsx",
        ".pptx",
        ".png",
        ".jpg",
        ".jpeg",
        ".zip"
    ];

    private readonly ISprintProjectDomain _projectDomain;
    private readonly ISprintProjectMemberDomain _projectMemberDomain;
    private readonly ISprintProjectMaterialDomain _materialDomain;
    private readonly ISprintProjectMaterialEventDomain _eventDomain;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public ProjectMaterialService(
        ISprintProjectDomain projectDomain,
        ISprintProjectMemberDomain projectMemberDomain,
        ISprintProjectMaterialDomain materialDomain,
        ISprintProjectMaterialEventDomain eventDomain,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _projectDomain = projectDomain;
        _projectMemberDomain = projectMemberDomain;
        _materialDomain = materialDomain;
        _eventDomain = eventDomain;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<SprintProjectMaterialListResult> ListAsync(
        string projectId,
        string? parentId,
        string? itemType,
        string? category,
        string? uploadedBy,
        string? keyword,
        int pageIndex,
        int pageSize,
        string userId,
        bool isSuperAdministrator)
    {
        await EnsureProjectReadableAsync(projectId, userId, isSuperAdministrator);
        var normalizedParentId = NormalizeOptional(parentId);
        if (!string.IsNullOrWhiteSpace(normalizedParentId))
        {
            await GetFolderOrThrowAsync(normalizedParentId, projectId);
        }

        var entities = await _materialDomain.ListAsync(entity => entity.ProjectId == projectId && entity.DeletedAt == null);
        var query = entities.AsEnumerable()
            .Where(entity => NormalizeOptional(entity.ParentId) == normalizedParentId);

        var normalizedItemType = NormalizeOptional(itemType);
        if (!string.IsNullOrWhiteSpace(normalizedItemType))
        {
            query = query.Where(entity => entity.ItemType == normalizedItemType);
        }

        var normalizedCategory = NormalizeOptional(category);
        if (!string.IsNullOrWhiteSpace(normalizedCategory))
        {
            query = query.Where(entity => string.Equals(entity.Category, normalizedCategory, StringComparison.OrdinalIgnoreCase));
        }

        var normalizedUploadedBy = NormalizeOptional(uploadedBy);
        if (!string.IsNullOrWhiteSpace(normalizedUploadedBy))
        {
            query = query.Where(entity => entity.UploadedBy == normalizedUploadedBy);
        }

        var normalizedKeyword = NormalizeOptional(keyword);
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            query = query.Where(entity =>
                entity.Name.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase) ||
                (entity.OriginalFileName?.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (entity.Description?.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var safePageIndex = Math.Max(pageIndex, 1);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);
        var ordered = query
            .OrderBy(entity => entity.ItemType == SprintProjectMaterialItemTypes.Folder ? 0 : 1)
            .ThenBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(entity => entity.CreateTime)
            .ToList();
        var total = ordered.Count;
        var items = ordered
            .Skip((safePageIndex - 1) * safePageSize)
            .Take(safePageSize)
            .Select(ToResult)
            .ToList();

        return new SprintProjectMaterialListResult(items, total, safePageIndex, safePageSize);
    }

    public async Task<SprintProjectMaterialResult> CreateFolderAsync(
        string projectId,
        CreateSprintProjectMaterialFolderRequest request,
        string userId,
        bool isSuperAdministrator)
    {
        await EnsureProjectWritableAsync(projectId, userId, isSuperAdministrator);
        var name = NormalizeName(request.Name);
        var parentId = NormalizeOptional(request.ParentId);
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            await GetFolderOrThrowAsync(parentId, projectId);
        }

        var siblings = await _materialDomain.ListAsync(entity =>
            entity.ProjectId == projectId &&
            entity.ItemType == SprintProjectMaterialItemTypes.Folder &&
            entity.DeletedAt == null);
        if (siblings.Any(entity =>
            NormalizeOptional(entity.ParentId) == parentId &&
            string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Folder name already exists in this directory.");
        }

        var entity = new SprintProjectMaterialEntity
        {
            ProjectId = projectId,
            ParentId = parentId,
            ItemType = SprintProjectMaterialItemTypes.Folder,
            Name = name,
            Category = NormalizeOptional(request.Category),
            TagsJson = SerializeTags(request.Tags),
            Description = NormalizeOptional(request.Description),
            UploadedBy = userId,
            ExtractStatus = SprintProjectMaterialExtractStatuses.None
        };

        await _materialDomain.CreateAsync(entity);
        await WriteEventAsync(projectId, entity.Id, SprintProjectMaterialEventTypes.FolderCreated, userId, new
        {
            entity.ParentId,
            entity.Name
        });
        return ToResult(entity);
    }

    public async Task<SprintProjectMaterialResult> UploadAsync(
        string projectId,
        string? parentId,
        string? category,
        IReadOnlyList<string>? tags,
        string? description,
        IFormFile file,
        string userId,
        bool isSuperAdministrator)
    {
        await EnsureProjectWritableAsync(projectId, userId, isSuperAdministrator);
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Upload file is empty.");
        }

        var normalizedParentId = NormalizeOptional(parentId);
        if (!string.IsNullOrWhiteSpace(normalizedParentId))
        {
            await GetFolderOrThrowAsync(normalizedParentId, projectId);
        }

        var originalName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new InvalidOperationException("Upload file extension is required.");
        }

        var options = ResolveOptions();
        if (file.Length > options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException($"Upload file exceeds {options.MaxFileSizeMb} MB.");
        }

        if (!options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Upload file extension is not allowed.");
        }

        var entity = new SprintProjectMaterialEntity
        {
            ProjectId = projectId,
            ParentId = normalizedParentId,
            ItemType = SprintProjectMaterialItemTypes.File,
            Name = NormalizeOptional(originalName) ?? $"material{extension}",
            OriginalFileName = NormalizeOptional(originalName),
            Extension = extension,
            ContentType = NormalizeOptional(file.ContentType) ?? "application/octet-stream",
            SizeBytes = file.Length,
            StorageRoot = SprintProjectMaterialStorageRoots.ApiRunDirectory,
            Category = NormalizeOptional(category),
            TagsJson = SerializeTags(tags),
            Description = NormalizeOptional(description),
            UploadedBy = userId,
            ExtractStatus = TextExtractExtensions.Contains(extension)
                ? SprintProjectMaterialExtractStatuses.Pending
                : SprintProjectMaterialExtractStatuses.Unsupported
        };

        var relativePath = Path.Combine(
            options.RelativeRootPath,
            projectId,
            "files",
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            $"{entity.Id}{extension}");
        var targetPath = ResolveMaterialPath(relativePath, options.RootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await using (var output = File.Create(targetPath))
        {
            await file.CopyToAsync(output);
        }

        entity.RelativePath = NormalizeRelativePath(relativePath);
        entity.Sha256 = await ComputeSha256Async(targetPath);

        await TryExtractTextAsync(entity, targetPath, options);
        await _materialDomain.CreateAsync(entity);
        await WriteEventAsync(projectId, entity.Id, SprintProjectMaterialEventTypes.Uploaded, userId, new
        {
            entity.ParentId,
            entity.Name,
            entity.OriginalFileName,
            entity.SizeBytes,
            entity.Extension,
            entity.ExtractStatus
        });

        return ToResult(entity);
    }

    public async Task<SprintProjectMaterialResult> GetAsync(
        string materialId,
        string userId,
        bool isSuperAdministrator)
    {
        var entity = await GetMaterialOrThrowAsync(materialId);
        await EnsureProjectReadableAsync(entity.ProjectId, userId, isSuperAdministrator);
        return ToResult(entity);
    }

    public async Task<ProjectMaterialDownloadResult> OpenDownloadAsync(
        string materialId,
        string userId,
        bool isSuperAdministrator)
    {
        var entity = await GetMaterialOrThrowAsync(materialId);
        await EnsureProjectReadableAsync(entity.ProjectId, userId, isSuperAdministrator);
        if (entity.ItemType != SprintProjectMaterialItemTypes.File)
        {
            throw new InvalidOperationException("Only files can be downloaded.");
        }

        if (string.IsNullOrWhiteSpace(entity.RelativePath))
        {
            throw new InvalidOperationException("Material file path is missing.");
        }

        var options = ResolveOptions();
        var path = ResolveMaterialPath(entity.RelativePath, options.RootPath);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("Material file does not exist.");
        }

        await WriteEventAsync(entity.ProjectId, entity.Id, SprintProjectMaterialEventTypes.Downloaded, userId, new
        {
            entity.Name,
            entity.SizeBytes
        });

        var stream = File.OpenRead(path);
        return new ProjectMaterialDownloadResult(
            stream,
            entity.OriginalFileName ?? entity.Name,
            entity.ContentType ?? "application/octet-stream",
            entity.SizeBytes);
    }

    public async Task<SprintProjectMaterialResult> UpdateAsync(
        string materialId,
        UpdateSprintProjectMaterialRequest request,
        string userId,
        bool isSuperAdministrator)
    {
        var entity = await GetMaterialOrThrowAsync(materialId);
        await EnsureProjectWritableAsync(entity.ProjectId, userId, isSuperAdministrator);
        var oldName = entity.Name;
        entity.Name = NormalizeName(request.Name);
        entity.Category = NormalizeOptional(request.Category);
        entity.TagsJson = SerializeTags(request.Tags);
        entity.Description = NormalizeOptional(request.Description);
        await _materialDomain.UpdateAsync(entity);
        await WriteEventAsync(entity.ProjectId, entity.Id, SprintProjectMaterialEventTypes.Renamed, userId, new
        {
            OldName = oldName,
            entity.Name,
            entity.Category
        });
        return ToResult(entity);
    }

    public async Task<SprintProjectMaterialResult> MoveAsync(
        string materialId,
        MoveSprintProjectMaterialRequest request,
        string userId,
        bool isSuperAdministrator)
    {
        var entity = await GetMaterialOrThrowAsync(materialId);
        await EnsureProjectWritableAsync(entity.ProjectId, userId, isSuperAdministrator);
        var newParentId = NormalizeOptional(request.ParentId);
        if (!string.IsNullOrWhiteSpace(newParentId))
        {
            var targetFolder = await GetFolderOrThrowAsync(newParentId, entity.ProjectId);
            if (entity.ItemType == SprintProjectMaterialItemTypes.Folder)
            {
                await EnsureFolderCanMoveAsync(entity, targetFolder);
            }
        }

        var oldParentId = entity.ParentId;
        entity.ParentId = newParentId;
        await _materialDomain.UpdateAsync(entity);
        await WriteEventAsync(entity.ProjectId, entity.Id, SprintProjectMaterialEventTypes.Moved, userId, new
        {
            OldParentId = oldParentId,
            NewParentId = newParentId
        });
        return ToResult(entity);
    }

    public async Task<bool> DeleteAsync(
        string materialId,
        string userId,
        bool isSuperAdministrator)
    {
        var entity = await GetMaterialOrThrowAsync(materialId);
        await EnsureProjectDeleteAsync(entity.ProjectId, userId, isSuperAdministrator);
        if (entity.ItemType == SprintProjectMaterialItemTypes.Folder)
        {
            var children = await _materialDomain.ListAsync(item =>
                item.ProjectId == entity.ProjectId &&
                item.ParentId == entity.Id &&
                item.DeletedAt == null);
            if (children.Count > 0)
            {
                throw new InvalidOperationException("Folder is not empty.");
            }
        }

        entity.DeletedAt = DateTime.UtcNow;
        entity.IsDelete = 1;
        await _materialDomain.UpdateAsync(entity);
        await WriteEventAsync(entity.ProjectId, entity.Id, SprintProjectMaterialEventTypes.Deleted, userId, new
        {
            entity.Name,
            entity.ItemType
        });
        return true;
    }

    private async Task TryExtractTextAsync(
        SprintProjectMaterialEntity entity,
        string filePath,
        ProjectMaterialStorageOptions options)
    {
        if (!TextExtractExtensions.Contains(entity.Extension ?? string.Empty))
        {
            return;
        }

        try
        {
            var text = await File.ReadAllTextAsync(filePath);
            var relativePath = Path.Combine(
                options.RelativeRootPath,
                entity.ProjectId,
                "extracted",
                $"{entity.Id}.txt");
            var targetPath = ResolveMaterialPath(relativePath, options.RootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, text);
            entity.ExtractedTextPath = NormalizeRelativePath(relativePath);
            entity.Summary = text.Length > 500 ? text[..500] : text;
            entity.ExtractStatus = SprintProjectMaterialExtractStatuses.Completed;
        }
        catch
        {
            entity.ExtractStatus = SprintProjectMaterialExtractStatuses.Failed;
        }
    }

    private async Task EnsureFolderCanMoveAsync(
        SprintProjectMaterialEntity movingFolder,
        SprintProjectMaterialEntity targetFolder)
    {
        if (movingFolder.Id == targetFolder.Id)
        {
            throw new InvalidOperationException("Folder cannot be moved into itself.");
        }

        var currentParentId = targetFolder.ParentId;
        while (!string.IsNullOrWhiteSpace(currentParentId))
        {
            if (currentParentId == movingFolder.Id)
            {
                throw new InvalidOperationException("Folder cannot be moved into its child folder.");
            }

            var parent = await _materialDomain.GetAsync(currentParentId);
            currentParentId = parent?.ParentId;
        }
    }

    private async Task<SprintProjectMaterialEntity> GetFolderOrThrowAsync(string folderId, string projectId)
    {
        var folder = await GetMaterialOrThrowAsync(folderId);
        if (folder.ProjectId != projectId ||
            folder.ItemType != SprintProjectMaterialItemTypes.Folder ||
            folder.DeletedAt is not null)
        {
            throw new InvalidOperationException("Target folder does not exist.");
        }

        return folder;
    }

    private async Task<SprintProjectMaterialEntity> GetMaterialOrThrowAsync(string id)
    {
        var entity = await _materialDomain.GetAsync(id);
        if (entity is null || entity.DeletedAt is not null)
        {
            throw new InvalidOperationException("Project material does not exist.");
        }

        return entity;
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
            throw new InvalidOperationException("No project material permission.");
        }
    }

    private async Task EnsureProjectWritableAsync(string projectId, string userId, bool isSuperAdministrator)
    {
        await EnsureProjectRoleAsync(
            projectId,
            userId,
            isSuperAdministrator,
            SprintProjectMemberRoles.ProjectManager,
            SprintProjectMemberRoles.Product,
            SprintProjectMemberRoles.Architect);
    }

    private async Task EnsureProjectDeleteAsync(string projectId, string userId, bool isSuperAdministrator)
    {
        await EnsureProjectRoleAsync(
            projectId,
            userId,
            isSuperAdministrator,
            SprintProjectMemberRoles.ProjectManager,
            SprintProjectMemberRoles.Architect);
    }

    private async Task EnsureProjectRoleAsync(
        string projectId,
        string userId,
        bool isSuperAdministrator,
        params string[] allowedRoles)
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
        if (!members.Any(entity => allowedRoles.Contains(entity.Role)))
        {
            throw new InvalidOperationException("No project material write permission.");
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

    private async Task WriteEventAsync(
        string projectId,
        string materialId,
        string eventType,
        string userId,
        object payload)
    {
        var entity = new SprintProjectMaterialEventEntity
        {
            ProjectId = projectId,
            MaterialId = materialId,
            EventType = eventType,
            CreatedBy = userId,
            PayloadJson = JsonSerializer.Serialize(payload)
        };
        await _eventDomain.CreateAsync(entity);
    }

    private ProjectMaterialStorageOptions ResolveOptions()
    {
        var configuredRelativeRoot = NormalizeOptional(_configuration["ProjectMaterials:RelativeRootPath"]) ?? "project-materials";
        var maxFileSizeMbText = NormalizeOptional(_configuration["ProjectMaterials:MaxFileSizeMb"]);
        var maxFileSizeMb = int.TryParse(maxFileSizeMbText, out var parsedMaxFileSizeMb)
            ? Math.Max(parsedMaxFileSizeMb, 1)
            : 50;
        var allowedExtensions = _configuration
            .GetSection("ProjectMaterials:AllowedExtensions")
            .Get<string[]>()?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.StartsWith('.') ? item.ToLowerInvariant() : $".{item.ToLowerInvariant()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var contentRoot = NormalizeOptional(_environment.ContentRootPath) ?? AppContext.BaseDirectory;
        var rootPath = Path.GetFullPath(contentRoot);
        return new ProjectMaterialStorageOptions(
            rootPath,
            configuredRelativeRoot.Trim().Trim('/', '\\'),
            maxFileSizeMb,
            allowedExtensions is { Length: > 0 } ? allowedExtensions : DefaultAllowedExtensions);
    }

    private static string ResolveMaterialPath(string relativePath, string rootPath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var fullRoot = Path.GetFullPath(rootPath);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelativePath));
        var rootRelativePath = Path.GetRelativePath(fullRoot, fullPath);
        if (Path.IsPathRooted(rootRelativePath) ||
            rootRelativePath == ".." ||
            rootRelativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            rootRelativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Material path is invalid.");
        }

        return fullPath;
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeName(string value)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Name is required.");
        }

        return Path.GetFileName(normalized);
    }

    private static string NormalizeRelativePath(string value)
    {
        return value.Replace('\\', '/').TrimStart('/');
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? SerializeTags(IReadOnlyList<string>? tags)
    {
        var normalized = tags?
            .Select(NormalizeOptional)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return normalized is { Count: > 0 }
            ? JsonSerializer.Serialize(normalized)
            : null;
    }

    private static IReadOnlyList<string> DeserializeTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(tagsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static SprintProjectMaterialResult ToResult(SprintProjectMaterialEntity entity)
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
            DeserializeTags(entity.TagsJson),
            entity.Description,
            entity.ExtractStatus,
            entity.ExtractedTextPath,
            entity.Summary,
            entity.UploadedBy,
            entity.DeletedAt,
            entity.UpdateTime,
            entity.CreateTime);
    }

    private sealed record ProjectMaterialStorageOptions(
        string RootPath,
        string RelativeRootPath,
        int MaxFileSizeMb,
        IReadOnlyList<string> AllowedExtensions)
    {
        public long MaxFileSizeBytes => MaxFileSizeMb * 1024L * 1024L;
    }
}
