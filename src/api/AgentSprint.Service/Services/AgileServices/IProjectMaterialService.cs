using AgentSprint.Model.Modules.Agile.Dtos;

using Microsoft.AspNetCore.Http;

namespace AgentSprint.Service.Services.AgileServices;

public interface IProjectMaterialService
{
    /// <summary>
    /// zh-cn: 查询项目材料列表，按项目、目录、类型、分类、上传人和关键词过滤，并返回分页结果。
    /// en-us: Lists project materials with project, folder, type, category, uploader, and keyword filters, returning a paged result.
    /// </summary>
    Task<SprintProjectMaterialListResult> ListAsync(
        string projectId,
        string? parentId,
        string? itemType,
        string? category,
        string? uploadedBy,
        string? keyword,
        int pageIndex,
        int pageSize,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 在项目材料中创建逻辑文件夹。
    /// en-us: Creates a logical folder under project materials.
    /// </summary>
    Task<SprintProjectMaterialResult> CreateFolderAsync(
        string projectId,
        CreateSprintProjectMaterialFolderRequest request,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 上传项目材料文件，文件内容保存到 API 运行目录，元数据落库。
    /// en-us: Uploads a project material file, stores the content under the API runtime directory, and persists metadata.
    /// </summary>
    Task<SprintProjectMaterialResult> UploadAsync(
        string projectId,
        string? parentId,
        string? category,
        IReadOnlyList<string>? tags,
        string? description,
        IFormFile file,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 获取单个项目材料详情。
    /// en-us: Gets one project material detail.
    /// </summary>
    Task<SprintProjectMaterialResult> GetAsync(
        string materialId,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 打开材料下载流，并返回下载文件名和内容类型。
    /// en-us: Opens a material download stream with download filename and content type.
    /// </summary>
    Task<ProjectMaterialDownloadResult> OpenDownloadAsync(
        string materialId,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 更新材料名称、分类、标签和说明。
    /// en-us: Updates material name, category, tags, and description.
    /// </summary>
    Task<SprintProjectMaterialResult> UpdateAsync(
        string materialId,
        UpdateSprintProjectMaterialRequest request,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 移动材料到新的逻辑目录。
    /// en-us: Moves a material item to a new logical folder.
    /// </summary>
    Task<SprintProjectMaterialResult> MoveAsync(
        string materialId,
        MoveSprintProjectMaterialRequest request,
        string userId,
        bool isSuperAdministrator);

    /// <summary>
    /// zh-cn: 软删除材料或空文件夹。
    /// en-us: Soft-deletes a material item or an empty folder.
    /// </summary>
    Task<bool> DeleteAsync(
        string materialId,
        string userId,
        bool isSuperAdministrator);
}

public sealed record ProjectMaterialDownloadResult(
    Stream Stream,
    string FileName,
    string ContentType,
    long SizeBytes);
