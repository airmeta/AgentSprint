using AgentSprint.Domain.Impls.Common;
using AgentSprint.Model.Modules.Tests;
using AgentSprint.Model.Modules.Tests.Domains;

using Air.Cloud.EntityFrameWork.Core.Repositories;

namespace AgentSprint.Domain.Impls.Tests;

public sealed class TestPlanDomain : EntityDomainBase<TestPlanEntity>, ITestPlanDomain
{
    /// <summary>
    /// zh-cn: 创建测试计划领域对象，并复用 Air.Cloud 仓储完成基础 CRUD。
    /// en-us: Creates the test plan domain and reuses the Air.Cloud repository for basic CRUD operations.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 测试计划实体仓储。
    /// en-us: Test plan entity repository.
    /// </param>
    public TestPlanDomain(IRepository<TestPlanEntity> repository) : base(repository)
    {
    }
}

public sealed class TestExecutionDomain : EntityDomainBase<TestExecutionEntity>, ITestExecutionDomain
{
    /// <summary>
    /// zh-cn: 创建测试执行领域对象，并复用 Air.Cloud 仓储完成基础 CRUD。
    /// en-us: Creates the test execution domain and reuses the Air.Cloud repository for basic CRUD operations.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 测试执行实体仓储。
    /// en-us: Test execution entity repository.
    /// </param>
    public TestExecutionDomain(IRepository<TestExecutionEntity> repository) : base(repository)
    {
    }
}
