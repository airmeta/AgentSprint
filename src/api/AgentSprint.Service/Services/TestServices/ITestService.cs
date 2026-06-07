using AgentSprint.Model.Modules.Tests.Dtos;

namespace AgentSprint.Service.Services.TestServices;

public interface ITestService
{
    /// <summary>
    /// zh-cn: 创建需求测试计划，默认环境为 test，并记录发起测试的用户。
    /// en-us: Creates a requirement test plan, defaults the environment to test, and records the initiating user.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 测试计划创建参数，ProjectId、RequirementId 和 Name 必填。
    /// en-us: Test plan creation payload; ProjectId, RequirementId, and Name are required.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前登录用户标识，将写入 CreatedBy。
    /// en-us: Current authenticated user identifier written to CreatedBy.
    /// </param>
    /// <returns>
    /// zh-cn: 已创建的测试计划结果。
    /// en-us: The created test plan result.
    /// </returns>
    Task<TestPlanResult> CreatePlanAsync(CreateTestPlanRequest request, string userId);

    /// <summary>
    /// zh-cn: 将测试计划置为 testing 状态，在首次启动时记录 StartedAt，并同步把绑定需求推进到测试中；当需求不存在时会抛出异常，避免计划和需求状态分叉。
    /// en-us: Marks a test plan as testing, records StartedAt the first time it starts, and moves the bound requirement into testing; throws when the requirement is missing to avoid split plan and requirement states.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 测试计划标识。
    /// en-us: Test plan identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的测试计划结果。
    /// en-us: The updated test plan result.
    /// </returns>
    Task<TestPlanResult> StartPlanAsync(string id);

    /// <summary>
    /// zh-cn: 完成测试计划，只允许 passed、failed、blocked 或 closed 作为完成态。
    /// en-us: Completes a test plan and only accepts passed, failed, blocked, or closed as terminal statuses.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 测试计划标识。
    /// en-us: Test plan identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 完成状态与总结；pending 和 testing 会被拒绝。
    /// en-us: Completion status and summary; pending and testing are rejected.
    /// </param>
    /// <returns>
    /// zh-cn: 完成后的测试计划结果。
    /// en-us: The completed test plan result.
    /// </returns>
    Task<TestPlanResult> CompletePlanAsync(string id, CompleteTestPlanRequest request);

    /// <summary>
    /// zh-cn: 按项目和需求筛选测试计划，未提供筛选值时返回全部未删除计划。
    /// en-us: Lists test plans filtered by project and requirement; omitted filters return all non-deleted plans.
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
    /// zh-cn: 按创建时间倒序排列的测试计划集合。
    /// en-us: Test plans ordered by creation time descending.
    /// </returns>
    Task<IReadOnlyList<TestPlanResult>> ListPlansAsync(string? projectId, string? requirementId);

    /// <summary>
    /// zh-cn: 提交一次测试执行结果，并根据执行结果同步测试计划状态；通过时把需求推进为已测试，失败结果必须绑定已有缺陷或本次创建缺陷，关联已有缺陷时会重开缺陷并把需求退回待修复。
    /// en-us: Submits a test execution result and synchronizes the test plan status; passed results move the requirement to tested, failed results must link an existing or newly created bug, and existing linked bugs are reopened while the requirement returns to pending-fix.
    /// </summary>
    /// <param name="testPlanId">
    /// zh-cn: 归属测试计划标识。
    /// en-us: Owning test plan identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 测试结果、实际结果、证据和关联 Bug 信息。
    /// en-us: Test result, actual result, evidence, and related bug information.
    /// </param>
    /// <param name="userId">
    /// zh-cn: 当前测试执行人标识。
    /// en-us: Current tester identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 已保存的测试执行结果。
    /// en-us: The saved test execution result.
    /// </returns>
    Task<TestExecutionResult> SubmitExecutionAsync(string testPlanId, SubmitTestExecutionRequest request, string userId);

    /// <summary>
    /// zh-cn: 查询指定测试计划的执行记录。
    /// en-us: Lists execution records for the specified test plan.
    /// </summary>
    /// <param name="testPlanId">
    /// zh-cn: 测试计划标识。
    /// en-us: Test plan identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 按执行时间倒序排列的执行记录集合。
    /// en-us: Execution records ordered by execution time descending.
    /// </returns>
    Task<IReadOnlyList<TestExecutionResult>> ListExecutionsAsync(string testPlanId);

    /// <summary>
    /// zh-cn: 回写测试执行记录关联的已有 Bug 或本次自动创建的 Bug，并在自动创建缺陷场景校验缺陷归属后写入测试计划和执行记录标识。
    /// en-us: Writes back the existing bug or newly created bug linked to a test execution, and for created defects validates ownership before writing the test plan and execution identifiers.
    /// </summary>
    /// <param name="testPlanId">
    /// zh-cn: 测试计划标识，用于校验执行记录归属。
    /// en-us: Test plan identifier used to validate execution ownership.
    /// </param>
    /// <param name="executionId">
    /// zh-cn: 测试执行记录标识。
    /// en-us: Test execution identifier.
    /// </param>
    /// <param name="request">
    /// zh-cn: 已有关联 Bug 或本次自动创建 Bug 的标识。
    /// en-us: Existing linked bug identifier or newly created bug identifier.
    /// </param>
    /// <returns>
    /// zh-cn: 更新后的测试执行记录。
    /// en-us: Updated test execution record.
    /// </returns>
    Task<TestExecutionResult> UpdateExecutionBugAsync(
        string testPlanId,
        string executionId,
        UpdateTestExecutionBugRequest request);
}
