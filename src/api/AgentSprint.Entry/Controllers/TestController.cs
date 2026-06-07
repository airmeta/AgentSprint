using System.Security.Claims;

using AgentSprint.Model.Modules.Tests.Dtos;
using AgentSprint.Service.Services.TestServices;

using Microsoft.AspNetCore.Mvc;

namespace AgentSprint.Entry.Controllers;

[ApiController]
[Route("test")]
public sealed class TestController : ControllerBase
{
    private readonly ITestService _testService;

    /// <summary>
    /// zh-cn: 创建测试控制器，所有操作委托给测试服务并使用当前登录用户作为操作人。
    /// en-us: Creates the test controller; all operations are delegated to the test service and use the current authenticated user as the actor.
    /// </summary>
    /// <param name="testService">
    /// zh-cn: 测试服务，用于创建计划、推进状态和记录执行结果。
    /// en-us: Test service used to create plans, advance statuses, and record execution results.
    /// </param>
    public TestController(ITestService testService)
    {
        _testService = testService;
    }

    /// <summary>
    /// zh-cn: 创建测试计划。请求参数无效时返回 400；成功时返回统一 ApiResponse 包装的计划数据。
    /// en-us: Creates a test plan. Invalid payloads return 400; successful calls return the plan data wrapped in ApiResponse.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 测试计划创建参数。
    /// en-us: Test plan creation payload.
    /// </param>
    /// <returns>
    /// zh-cn: 操作结果和测试计划数据。
    /// en-us: Operation result and test plan data.
    /// </returns>
    [HttpPost("plans")]
    public async Task<ActionResult<ApiResponse<TestPlanResult>>> CreatePlan(CreateTestPlanRequest request)
    {
        try
        {
            return ApiResponse<TestPlanResult>.Ok(await _testService.CreatePlanAsync(request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<TestPlanResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 查询测试计划列表，可按项目和需求筛选。
    /// en-us: Lists test plans with optional project and requirement filters.
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
    /// zh-cn: 测试计划列表。
    /// en-us: Test plan list.
    /// </returns>
    [HttpGet("plans")]
    public async Task<ApiResponse<IReadOnlyList<TestPlanResult>>> ListPlans(
        [FromQuery] string? projectId,
        [FromQuery] string? requirementId)
    {
        return ApiResponse<IReadOnlyList<TestPlanResult>>.Ok(
            await _testService.ListPlansAsync(projectId, requirementId));
    }

    /// <summary>
    /// zh-cn: 启动测试计划并进入 testing 状态。
    /// en-us: Starts a test plan and moves it to the testing status.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 测试计划标识。
    /// en-us: Test plan identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的测试计划。
    /// en-us: Updated test plan.
    /// </returns>
    [HttpPost("plans/{id}/start")]
    public async Task<ActionResult<ApiResponse<TestPlanResult>>> StartPlan(string id)
    {
        try
        {
            return ApiResponse<TestPlanResult>.Ok(await _testService.StartPlanAsync(id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<TestPlanResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 完成测试计划并写入测试总结。
    /// en-us: Completes a test plan and stores its test summary.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 测试计划标识。
    /// en-us: Test plan identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 完成状态与总结。
    /// en-us: Completion status and summary.
    /// </param>
    /// <returns>
    /// zh-cn: 完成后的测试计划。
    /// en-us: Completed test plan.
    /// </returns>
    [HttpPost("plans/{id}/complete")]
    public async Task<ActionResult<ApiResponse<TestPlanResult>>> CompletePlan(
        string id,
        CompleteTestPlanRequest request)
    {
        try
        {
            return ApiResponse<TestPlanResult>.Ok(await _testService.CompletePlanAsync(id, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<TestPlanResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 提交测试执行记录，并同步测试计划状态。
    /// en-us: Submits a test execution record and synchronizes the test plan status.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 测试计划标识。
    /// en-us: Test plan identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 执行结果、证据和关联 Bug 信息。
    /// en-us: Execution result, evidence, and related bug information.
    /// </param>
    /// <returns>
    /// zh-cn: 已保存的测试执行记录。
    /// en-us: Saved test execution record.
    /// </returns>
    [HttpPost("plans/{id}/executions")]
    public async Task<ActionResult<ApiResponse<TestExecutionResult>>> SubmitExecution(
        string id,
        SubmitTestExecutionRequest request)
    {
        try
        {
            return ApiResponse<TestExecutionResult>.Ok(
                await _testService.SubmitExecutionAsync(id, request, GetUserId()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<TestExecutionResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>
    /// zh-cn: 查询指定测试计划的执行记录。
    /// en-us: Lists execution records for the specified test plan.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 测试计划标识。
    /// en-us: Test plan identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 测试执行记录列表。
    /// en-us: Test execution record list.
    /// </returns>
    [HttpGet("plans/{id}/executions")]
    public async Task<ApiResponse<IReadOnlyList<TestExecutionResult>>> ListExecutions(string id)
    {
        return ApiResponse<IReadOnlyList<TestExecutionResult>>.Ok(await _testService.ListExecutionsAsync(id));
    }

    /// <summary>
    /// zh-cn: 回写测试执行记录关联的 Bug。
    /// en-us: Updates the bug linkage for a test execution record.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 测试计划标识。
    /// en-us: Test plan identifier.
    /// </param>
    /// <param name="executionId">
    /// zh-cn: 测试执行记录标识。
    /// en-us: Test execution identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: Bug 关联参数。
    /// en-us: Bug linkage payload.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的测试执行记录。
    /// en-us: Updated test execution record.
    /// </returns>
    [HttpPut("plans/{id}/executions/{executionId}/bug")]
    public async Task<ActionResult<ApiResponse<TestExecutionResult>>> UpdateExecutionBug(
        string id,
        string executionId,
        UpdateTestExecutionBugRequest request)
    {
        try
        {
            return ApiResponse<TestExecutionResult>.Ok(
                await _testService.UpdateExecutionBugAsync(id, executionId, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<TestExecutionResult>.Error(ex.Message, 400));
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    }
}
