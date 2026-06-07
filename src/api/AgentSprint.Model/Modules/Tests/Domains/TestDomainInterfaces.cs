using AgentSprint.Model.Modules.Security.Domains;

namespace AgentSprint.Model.Modules.Tests.Domains;

public interface ITestPlanDomain : IEntityDomainBase<TestPlanEntity>;

public interface ITestExecutionDomain : IEntityDomainBase<TestExecutionEntity>;
