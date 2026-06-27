using System.Security.Claims;

using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Service.Services.AgileServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Authorize]
[Route("mvp")]
public sealed class ProposalController : ControllerBase
{
    private readonly IProposalService _proposalService;

    public ProposalController(IProposalService proposalService)
    {
        _proposalService = proposalService;
    }

    [HttpGet("projects/{projectId}/proposals")]
    public async Task<ActionResult<ApiResponse<SprintProposalListResult>>> ListProposals(
        string projectId,
        [FromQuery] string? status = null,
        [FromQuery] string? createdBy = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            return ApiResponse<SprintProposalListResult>.Ok(
                await _proposalService.ListAsync(
                    projectId,
                    status,
                    createdBy,
                    keyword,
                    pageIndex,
                    pageSize,
                    GetUserId(),
                    IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProposalListResult>.Error(ex.Message, 400));
        }
    }

    [HttpPost("projects/{projectId}/proposals")]
    public async Task<ActionResult<ApiResponse<SprintProposalResult>>> CreateProposal(
        string projectId,
        CreateSprintProposalRequest request)
    {
        try
        {
            return ApiResponse<SprintProposalResult>.Ok(
                await _proposalService.CreateAsync(
                    projectId,
                    request,
                    GetUserId(),
                    IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProposalResult>.Error(ex.Message, 400));
        }
    }

    [HttpGet("proposals/{proposalId}")]
    public async Task<ActionResult<ApiResponse<SprintProposalResult>>> GetProposal(string proposalId)
    {
        try
        {
            return ApiResponse<SprintProposalResult>.Ok(
                await _proposalService.GetAsync(proposalId, GetUserId(), IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProposalResult>.Error(ex.Message, 400));
        }
    }

    [HttpPut("proposals/{proposalId}")]
    public async Task<ActionResult<ApiResponse<SprintProposalResult>>> UpdateProposal(
        string proposalId,
        UpdateSprintProposalRequest request)
    {
        try
        {
            return ApiResponse<SprintProposalResult>.Ok(
                await _proposalService.UpdateAsync(
                    proposalId,
                    request,
                    GetUserId(),
                    IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProposalResult>.Error(ex.Message, 400));
        }
    }

    [HttpPost("proposals/{proposalId}/confirm")]
    public async Task<ActionResult<ApiResponse<SprintProposalResult>>> ConfirmProposal(string proposalId)
    {
        try
        {
            return ApiResponse<SprintProposalResult>.Ok(
                await _proposalService.ConfirmAsync(proposalId, GetUserId(), IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProposalResult>.Error(ex.Message, 400));
        }
    }

    [HttpPost("proposals/{proposalId}/void")]
    public async Task<ActionResult<ApiResponse<SprintProposalResult>>> VoidProposal(string proposalId)
    {
        try
        {
            return ApiResponse<SprintProposalResult>.Ok(
                await _proposalService.VoidAsync(proposalId, GetUserId(), IsSuperAdministrator()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SprintProposalResult>.Error(ex.Message, 400));
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
}
