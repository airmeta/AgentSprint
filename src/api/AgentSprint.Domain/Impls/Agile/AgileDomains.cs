using AgentSprint.Domain.Impls.Common;
using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Agile.Workers;

using Air.Cloud.EntityFrameWork.Core.Repositories;

namespace AgentSprint.Domain.Impls.Agile;

public sealed class SprintProjectDomain : EntityDomainBase<SprintProjectEntity>, ISprintProjectDomain
{
    /// <summary>
    /// zh-cn: 创建项目领域对象，并复用 Air.Cloud 仓储完成项目基础持久化。
    /// en-us: Creates the project domain and reuses the Air.Cloud repository for basic project persistence.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 项目实体仓储。
    /// en-us: Project entity repository.
    /// </param>
    public SprintProjectDomain(IRepository<SprintProjectEntity> repository) : base(repository)
    {
    }
}

public sealed class GitAccountDomain : EntityDomainBase<GitAccountEntity>, IGitAccountDomain
{
    /// <summary>
    /// zh-cn: 创建 Git 账户领域对象，复用 Air.Cloud 仓储维护仓库访问账号和令牌信息。
    /// en-us: Creates the Git account domain and reuses the Air.Cloud repository to maintain repository access accounts and tokens.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: Git 账户实体仓储。
    /// en-us: Git account entity repository.
    /// </param>
    public GitAccountDomain(IRepository<GitAccountEntity> repository) : base(repository)
    {
    }
}

public sealed class GitRepositoryDomain : EntityDomainBase<GitRepositoryEntity>, IGitRepositoryDomain
{
    /// <summary>
    /// zh-cn: 创建 Git 仓库领域对象，复用 Air.Cloud 仓储维护仓库地址、默认分支、关联账户和本地路径。
    /// en-us: Creates the Git repository domain and reuses the Air.Cloud repository to maintain repository URL, default branch, linked account, and local path.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: Git 仓库实体仓储。
    /// en-us: Git repository entity repository.
    /// </param>
    public GitRepositoryDomain(IRepository<GitRepositoryEntity> repository) : base(repository)
    {
    }
}

public sealed class GitBranchOperationDomain :
    EntityDomainBase<GitBranchOperationEntity>,
    IGitBranchOperationDomain
{
    /// <summary>
    /// zh-cn: 创建 Git 分支操作领域对象，复用 Air.Cloud 仓储记录新增分支、备份删除分支和推送读取结果。
    /// en-us: Creates the Git branch-operation domain and reuses the Air.Cloud repository to record branch creation, backup-before-delete, and push-log reads.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: Git 分支操作实体仓储。
    /// en-us: Git branch operation entity repository.
    /// </param>
    public GitBranchOperationDomain(IRepository<GitBranchOperationEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintProjectMemberDomain :
    EntityDomainBase<SprintProjectMemberEntity>,
    ISprintProjectMemberDomain
{
    /// <summary>
    /// zh-cn: 创建项目成员领域对象，复用 Air.Cloud 仓储维护项目参与人和项目内角色关系。
    /// en-us: Creates the project-member domain and reuses the Air.Cloud repository to maintain project participants and their project roles.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 项目成员实体仓储。
    /// en-us: Project-member entity repository.
    /// </param>
    public SprintProjectMemberDomain(IRepository<SprintProjectMemberEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintProjectEndpointDomain :
    EntityDomainBase<SprintProjectEndpointEntity>,
    ISprintProjectEndpointDomain
{
    /// <summary>
    /// zh-cn: 鍒涘缓椤圭洰绔鍩熷璞★紝澶嶇敤 Air.Cloud 浠撳偍缁存姢椤圭洰涓嬬殑 IOS銆佸畨鍗撱€乄eb 绛夌淇℃伅銆?    /// en-us: Creates the project-endpoint domain and reuses the Air.Cloud repository to maintain iOS, Android, web, and other endpoint records under a project.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 椤圭洰绔疄浣撲粨鍌ㄣ€?    /// en-us: Project-endpoint entity repository.
    /// </param>
    public SprintProjectEndpointDomain(IRepository<SprintProjectEndpointEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintFeatureModuleDomain :
    EntityDomainBase<SprintFeatureModuleEntity>,
    ISprintFeatureModuleDomain
{
    /// <summary>
    /// zh-cn: 鍒涘缓鍔熻兘妯″潡棰嗗煙瀵硅薄锛屽鐢?Air.Cloud 浠撳偍缁存姢绔笅鐨勫姛鑳芥ā鍧楋紝渚涢渶姹傚拰寤鸿缁戝畾銆?    /// en-us: Creates the feature-module domain and reuses the Air.Cloud repository to maintain endpoint modules used by requirements and suggestions.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 鍔熻兘妯″潡瀹炰綋浠撳偍銆?    /// en-us: Feature-module entity repository.
    /// </param>
    public SprintFeatureModuleDomain(IRepository<SprintFeatureModuleEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintRequirementDomain : EntityDomainBase<SprintRequirementEntity>, ISprintRequirementDomain
{
    /// <summary>
    /// zh-cn: 创建需求领域对象，并复用 Air.Cloud 仓储完成需求基础持久化。
    /// en-us: Creates the requirement domain and reuses the Air.Cloud repository for basic requirement persistence.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 需求实体仓储。
    /// en-us: Requirement entity repository.
    /// </param>
    public SprintRequirementDomain(IRepository<SprintRequirementEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintSkillDomain : EntityDomainBase<SprintSkillEntity>, ISprintSkillDomain
{
    /// <summary>
    /// zh-cn: 创建 Skill 配置领域对象，复用 Air.Cloud 仓储维护可选的 Codex Skill 指令内容。
    /// en-us: Creates the skill-configuration domain and reuses the Air.Cloud repository to maintain selectable Codex skill instructions.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: Skill 配置实体仓储。
    /// en-us: Skill configuration entity repository.
    /// </param>
    public SprintSkillDomain(IRepository<SprintSkillEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintFeatureSuggestionDomain :
    EntityDomainBase<SprintFeatureSuggestionEntity>,
    ISprintFeatureSuggestionDomain
{
    /// <summary>
    /// zh-cn: 鍒涘缓鍔熻兘寤鸿棰嗗煙瀵硅薄锛屽鐢?Air.Cloud 浠撳偍淇濆瓨椤圭洰鎴愬憳鎻愪氦鐨勬ā鍧楀寲浜у搧寤鸿銆?    /// en-us: Creates the feature-suggestion domain and reuses the Air.Cloud repository to save module-scoped product suggestions submitted by project participants.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 鍔熻兘寤鸿瀹炰綋浠撳偍銆?    /// en-us: Feature-suggestion entity repository.
    /// </param>
    public SprintFeatureSuggestionDomain(IRepository<SprintFeatureSuggestionEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintRequirementFeedbackDomain :
    EntityDomainBase<SprintRequirementFeedbackEntity>,
    ISprintRequirementFeedbackDomain
{
    /// <summary>
    /// zh-cn: 创建需求回馈领域对象，复用 Air.Cloud 仓储维护验收后的产品回馈以及回馈转后续需求的追踪关系。
    /// en-us: Creates the requirement-feedback domain and reuses the Air.Cloud repository to maintain post-acceptance product feedback and its conversion into follow-up requirements.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 需求回馈实体仓储。
    /// en-us: Requirement-feedback entity repository.
    /// </param>
    public SprintRequirementFeedbackDomain(IRepository<SprintRequirementFeedbackEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintRequirementReviewDomain :
    EntityDomainBase<SprintRequirementReviewEntity>,
    ISprintRequirementReviewDomain
{
    /// <summary>
    /// zh-cn: 创建需求评审领域对象，复用 Air.Cloud 仓储维护每个评审人的评审状态。
    /// en-us: Creates the requirement-review domain and reuses the Air.Cloud repository to maintain each reviewer's decision state.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 需求评审实体仓储。
    /// en-us: Requirement-review entity repository.
    /// </param>
    public SprintRequirementReviewDomain(IRepository<SprintRequirementReviewEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintDevelopmentTaskDomain :
    EntityDomainBase<SprintDevelopmentTaskEntity>,
    ISprintDevelopmentTaskDomain
{
    /// <summary>
    /// zh-cn: 创建开发任务领域对象，复用 Air.Cloud 仓储维护需求拆解后的任务明细。
    /// en-us: Creates the development-task domain and reuses the Air.Cloud repository to maintain task details generated from requirement decomposition.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 开发任务实体仓储。
    /// en-us: Development-task entity repository.
    /// </param>
    public SprintDevelopmentTaskDomain(IRepository<SprintDevelopmentTaskEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintBugDomain : EntityDomainBase<SprintBugEntity>, ISprintBugDomain
{
    /// <summary>
    /// zh-cn: 创建 Bug 领域对象，并复用 Air.Cloud 仓储完成 Bug 基础持久化。
    /// en-us: Creates the bug domain and reuses the Air.Cloud repository for basic bug persistence.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: Bug 实体仓储。
    /// en-us: Bug entity repository.
    /// </param>
    public SprintBugDomain(IRepository<SprintBugEntity> repository) : base(repository)
    {
    }
}

public sealed class SprintTaskLeaseDomain : EntityDomainBase<SprintTaskLeaseEntity>, ISprintTaskLeaseDomain
{
    /// <summary>
    /// zh-cn: 创建任务租约领域对象，并复用 Air.Cloud 仓储完成租约基础持久化。
    /// en-us: Creates the task lease domain and reuses the Air.Cloud repository for basic lease persistence.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 任务租约实体仓储。
    /// en-us: Task lease entity repository.
    /// </param>
    public SprintTaskLeaseDomain(IRepository<SprintTaskLeaseEntity> repository) : base(repository)
    {
    }
}

public sealed class DigitalWorkerDomain : EntityDomainBase<DigitalWorkerEntity>, IDigitalWorkerDomain
{
    /// <summary>
    /// zh-cn: 创建数字员工主档领域对象，复用 Air.Cloud 仓储维护 Worker 管理配置。
    /// en-us: Creates the digital-worker domain and reuses the Air.Cloud repository to maintain Worker management configuration.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: 数字员工主档实体仓储。
    /// en-us: Digital-worker entity repository.
    /// </param>
    public DigitalWorkerDomain(IRepository<DigitalWorkerEntity> repository) : base(repository)
    {
    }
}

public sealed class WorkerSessionDomain : EntityDomainBase<WorkerSessionEntity>, IWorkerSessionDomain
{
    /// <summary>
    /// zh-cn: 创建 Worker 会话领域对象，用于记录 AgentSprint.Worker 进程生命周期和心跳状态。
    /// en-us: Creates the Worker-session domain used to record AgentSprint.Worker process lifecycles and heartbeat state.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: Worker 会话实体仓储。
    /// en-us: Worker-session entity repository.
    /// </param>
    public WorkerSessionDomain(IRepository<WorkerSessionEntity> repository) : base(repository)
    {
    }
}

public sealed class WorkerCommandDomain : EntityDomainBase<WorkerCommandEntity>, IWorkerCommandDomain
{
    /// <summary>
    /// zh-cn: 创建 Worker 命令领域对象，用于维护平台下发给受控端的命令队列。
    /// en-us: Creates the Worker-command domain used to maintain commands issued by the platform to controlled workers.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: Worker 命令实体仓储。
    /// en-us: Worker-command entity repository.
    /// </param>
    public WorkerCommandDomain(IRepository<WorkerCommandEntity> repository) : base(repository)
    {
    }
}

public sealed class WorkerRunDomain : EntityDomainBase<WorkerRunEntity>, IWorkerRunDomain
{
    /// <summary>
    /// zh-cn: 创建 Worker 运行记录领域对象，用于保存 Codex 执行摘要和本地产物路径。
    /// en-us: Creates the Worker-run domain used to store Codex execution summaries and local artifact paths.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: Worker 运行记录实体仓储。
    /// en-us: Worker-run entity repository.
    /// </param>
    public WorkerRunDomain(IRepository<WorkerRunEntity> repository) : base(repository)
    {
    }
}

public sealed class WorkerEventDomain : EntityDomainBase<WorkerEventEntity>, IWorkerEventDomain
{
    /// <summary>
    /// zh-cn: 创建 Worker 事件领域对象，用于保存受控端生命周期、命令和运行过程审计事件。
    /// en-us: Creates the Worker-event domain used to store audit events for controlled-worker lifecycles, commands, and runs.
    /// </summary>
    /// <param name="repository">
    /// zh-cn: Worker 事件实体仓储。
    /// en-us: Worker-event entity repository.
    /// </param>
    public WorkerEventDomain(IRepository<WorkerEventEntity> repository) : base(repository)
    {
    }
}
