using System.Security.Claims;

using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Authorize]
[Route("mvp")]
public sealed class ProjectMaterialController : ControllerBase
{
    private readonly IProjectMaterialService _projectMaterialService;

    public ProjectMaterialController(IProjectMaterialService projectMaterialService)
    {
        _projectMaterialService = projectMaterialService;
    }

    [HttpGet("projects/{projectId}/materials")]
    public async Task<ActionResult<ApiResponse<SprintProjectMaterialListResult>>> ListMaterials(
        string projectId,
        [FromQuery] string? parentId = null,
        [FromQuery] string? itemType = null,
        [FromQuery] string? category = null,
        [FromQuery] string? uploadedBy = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            return ApiResponse<SprintProjectMaterialListResult>.Ok(
                await _projectMaterialService.ListAsync(
                    projectId,
                    parentId,
                    itemType,
                    category,
                    uploadedBy,
                    keyword,
                    pageIndex,
                    pageSize,
                    GetUserId(),
                    IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProjectMaterialListResult>.Error(ex.Message, 400));
        }
    }

    [HttpPost("projects/{projectId}/materials/folders")]
    public async Task<ActionResult<ApiResponse<SprintProjectMaterialResult>>> CreateFolder(
        string projectId,
        CreateSprintProjectMaterialFolderRequest request)
    {
        try
        {
            return ApiResponse<SprintProjectMaterialResult>.Ok(
                await _projectMaterialService.CreateFolderAsync(
                    projectId,
                    request,
                    GetUserId(),
                    IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProjectMaterialResult>.Error(ex.Message, 400));
        }
    }

    [HttpPost("projects/{projectId}/materials/upload")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<SprintProjectMaterialResult>>> Upload(
        string projectId,
        [FromForm] string? parentId,
        [FromForm] string? category,
        [FromForm] string? tags,
        [FromForm] string? description,
        IFormFile file)
    {
        try
        {
            return ApiResponse<SprintProjectMaterialResult>.Ok(
                await _projectMaterialService.UploadAsync(
                    projectId,
                    parentId,
                    category,
                    ParseTags(tags),
                    description,
                    file,
                    GetUserId(),
                    IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProjectMaterialResult>.Error(ex.Message, 400));
        }
    }

    [HttpGet("materials/{materialId}")]
    public async Task<ActionResult<ApiResponse<SprintProjectMaterialResult>>> GetMaterial(string materialId)
    {
        try
        {
            return ApiResponse<SprintProjectMaterialResult>.Ok(
                await _projectMaterialService.GetAsync(materialId, GetUserId(), IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProjectMaterialResult>.Error(ex.Message, 400));
        }
    }

    [HttpGet("materials/{materialId}/download")]
    public async Task<IActionResult> Download(string materialId)
    {
        try
        {
            var result = await _projectMaterialService.OpenDownloadAsync(
                materialId,
                GetUserId(),
                IsSuperAdministrator());
            return File(result.Stream, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message, 400));
        }
    }

    [HttpPut("materials/{materialId}")]
    public async Task<ActionResult<ApiResponse<SprintProjectMaterialResult>>> Update(
        string materialId,
        UpdateSprintProjectMaterialRequest request)
    {
        try
        {
            return ApiResponse<SprintProjectMaterialResult>.Ok(
                await _projectMaterialService.UpdateAsync(
                    materialId,
                    request,
                    GetUserId(),
                    IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProjectMaterialResult>.Error(ex.Message, 400));
        }
    }

    [HttpPut("materials/{materialId}/move")]
    public async Task<ActionResult<ApiResponse<SprintProjectMaterialResult>>> Move(
        string materialId,
        MoveSprintProjectMaterialRequest request)
    {
        try
        {
            return ApiResponse<SprintProjectMaterialResult>.Ok(
                await _projectMaterialService.MoveAsync(
                    materialId,
                    request,
                    GetUserId(),
                    IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProjectMaterialResult>.Error(ex.Message, 400));
        }
    }

    [HttpDelete("materials/{materialId}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(string materialId)
    {
        try
        {
            return ApiResponse<bool>.Ok(
                await _projectMaterialService.DeleteAsync(
                    materialId,
                    GetUserId(),
                    IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.Error(ex.Message, 400));
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            "system";
    }

    private bool IsSuperAdministrator()
    {
        return User.IsInRole("admin") ||
            User.IsInRole("administrator") ||
            User.IsInRole("super_admin");
    }

    private static IReadOnlyList<string> ParseTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return [];
        }

        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
