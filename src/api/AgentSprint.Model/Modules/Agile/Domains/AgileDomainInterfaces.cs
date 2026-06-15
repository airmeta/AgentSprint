using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Agile.Workers;

namespace AgentSprint.Model.Modules.Agile.Domains;

public interface ISprintProjectDomain : IEntityDomainBase<SprintProjectEntity>;

public interface IGitAccountDomain : IEntityDomainBase<GitAccountEntity>;

public interface IGitRepositoryDomain : IEntityDomainBase<GitRepositoryEntity>;

public interface IGitBranchOperationDomain : IEntityDomainBase<GitBranchOperationEntity>;

public interface ISprintProjectMemberDomain : IEntityDomainBase<SprintProjectMemberEntity>;

public interface ISprintProjectEndpointDomain : IEntityDomainBase<SprintProjectEndpointEntity>;

public interface ISprintFeatureModuleDomain : IEntityDomainBase<SprintFeatureModuleEntity>;

public interface ISprintRequirementDomain : IEntityDomainBase<SprintRequirementEntity>;

public interface ISprintSkillDomain : IEntityDomainBase<SprintSkillEntity>;

public interface ISprintFeatureSuggestionDomain : IEntityDomainBase<SprintFeatureSuggestionEntity>;

public interface ISprintRequirementFeedbackDomain : IEntityDomainBase<SprintRequirementFeedbackEntity>;

public interface ISprintRequirementReviewDomain : IEntityDomainBase<SprintRequirementReviewEntity>;

public interface ISprintDevelopmentTaskDomain : IEntityDomainBase<SprintDevelopmentTaskEntity>;

public interface ISprintBugDomain : IEntityDomainBase<SprintBugEntity>;

public interface ISprintTaskLeaseDomain : IEntityDomainBase<SprintTaskLeaseEntity>;

public interface IDigitalWorkerDomain : IEntityDomainBase<DigitalWorkerEntity>;

public interface IWorkerSessionDomain : IEntityDomainBase<WorkerSessionEntity>;

public interface IWorkerCommandDomain : IEntityDomainBase<WorkerCommandEntity>;

public interface IWorkerRunDomain : IEntityDomainBase<WorkerRunEntity>;

public interface IWorkerEventDomain : IEntityDomainBase<WorkerEventEntity>;
