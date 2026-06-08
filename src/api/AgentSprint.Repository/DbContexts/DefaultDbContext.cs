using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Tests;

using Microsoft.EntityFrameworkCore;

namespace AgentSprint.Repository.DbContexts;

[AppDbContext("#(ConnectionStrings:AgentSprintConnectionString)")]
public sealed class DefaultDbContext : AppDbContext<DefaultDbContext>
{
    /// <summary>
    /// zh-cn: 创建默认业务数据库上下文，承载安全模块和测试模块实体集合。
    /// en-us: Creates the default business database context that hosts security-module and test-module entity sets.
    /// </summary>
    /// <param name="options">
    /// zh-cn: EF Core 上下文配置，由 Air.Cloud 数据库访问器注入。
    /// en-us: EF Core context options injected by the Air.Cloud database accessor.
    /// </param>
    public DefaultDbContext(DbContextOptions<DefaultDbContext> options) : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<RoleEntity> Roles => Set<RoleEntity>();

    public DbSet<MenuEntity> Menus => Set<MenuEntity>();

    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();

    public DbSet<AgentTokenEntity> AgentTokens => Set<AgentTokenEntity>();

    public DbSet<SystemConfigurationEntity> SystemConfigurations => Set<SystemConfigurationEntity>();

    public DbSet<UserGroupEntity> UserGroups => Set<UserGroupEntity>();

    public DbSet<RoleGroupEntity> RoleGroups => Set<RoleGroupEntity>();

    public DbSet<DepartmentEntity> Departments => Set<DepartmentEntity>();

    public DbSet<AssignmentEntity> Assignments => Set<AssignmentEntity>();

    public DbSet<DictionaryTypeEntity> DictionaryTypes => Set<DictionaryTypeEntity>();

    public DbSet<DictionaryItemEntity> DictionaryItems => Set<DictionaryItemEntity>();

    public DbSet<RuntimeEnvironmentEntity> RuntimeEnvironments => Set<RuntimeEnvironmentEntity>();

    public DbSet<RuntimeEnvironmentContainerEntity> RuntimeEnvironmentContainers => Set<RuntimeEnvironmentContainerEntity>();

    public DbSet<PromptTemplateEntity> PromptTemplates => Set<PromptTemplateEntity>();

    public DbSet<EntityAssociationEntity> EntityAssociations => Set<EntityAssociationEntity>();

    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();

    public DbSet<RoleMenuEntity> RoleMenus => Set<RoleMenuEntity>();

    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();

    public DbSet<SprintProjectEntity> SprintProjects => Set<SprintProjectEntity>();

    public DbSet<SprintProjectMemberEntity> SprintProjectMembers => Set<SprintProjectMemberEntity>();

    public DbSet<SprintProjectEndpointEntity> SprintProjectEndpoints => Set<SprintProjectEndpointEntity>();

    public DbSet<SprintFeatureModuleEntity> SprintFeatureModules => Set<SprintFeatureModuleEntity>();

    public DbSet<SprintRequirementEntity> SprintRequirements => Set<SprintRequirementEntity>();

    public DbSet<SprintSkillEntity> SprintSkills => Set<SprintSkillEntity>();

    public DbSet<SprintFeatureSuggestionEntity> SprintFeatureSuggestions => Set<SprintFeatureSuggestionEntity>();

    public DbSet<SprintRequirementFeedbackEntity> SprintRequirementFeedbacks => Set<SprintRequirementFeedbackEntity>();

    public DbSet<SprintRequirementReviewEntity> SprintRequirementReviews => Set<SprintRequirementReviewEntity>();

    public DbSet<SprintDevelopmentTaskEntity> SprintDevelopmentTasks => Set<SprintDevelopmentTaskEntity>();

    public DbSet<SprintBugEntity> SprintBugs => Set<SprintBugEntity>();

    public DbSet<SprintTaskLeaseEntity> SprintTaskLeases => Set<SprintTaskLeaseEntity>();

    public DbSet<TestPlanEntity> TestPlans => Set<TestPlanEntity>();

    public DbSet<TestExecutionEntity> TestExecutions => Set<TestExecutionEntity>();

    /// <summary>
    /// zh-cn: 配置安全模块唯一索引和测试模块查询索引，保证登录、授权和测试计划查询具备稳定约束。
    /// en-us: Configures unique indexes for the security module and query indexes for the test module to keep login, authorization, and test plan queries constrained and predictable.
    /// </summary>
    /// <param name="modelBuilder">
    /// zh-cn: EF Core 模型构建器。
    /// en-us: EF Core model builder.
    /// </param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>().HasIndex(entity => entity.Username).IsUnique();
        modelBuilder.Entity<RoleEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<PermissionEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<AgentTokenEntity>().HasIndex(entity => entity.TokenHash).IsUnique();
        modelBuilder.Entity<AgentTokenEntity>().HasIndex(entity => new { entity.OwnerUserId, entity.Status });
        modelBuilder.Entity<AgentTokenEntity>().HasIndex(entity => entity.ProjectId);
        modelBuilder.Entity<SystemConfigurationEntity>().HasIndex(entity => entity.Key).IsUnique();
        modelBuilder.Entity<UserGroupEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<RoleGroupEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<DepartmentEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<AssignmentEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<DictionaryTypeEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<DictionaryItemEntity>().HasIndex(entity => new { entity.DictionaryTypeId, entity.Code }).IsUnique();
        modelBuilder.Entity<DictionaryItemEntity>().HasIndex(entity => entity.DictionaryTypeId);
        modelBuilder.Entity<RuntimeEnvironmentEntity>().HasIndex(entity => new { entity.ProjectId, entity.Code }).IsUnique();
        modelBuilder.Entity<RuntimeEnvironmentEntity>().HasIndex(entity => new { entity.ProjectId, entity.EndpointId, entity.ModuleId });
        modelBuilder.Entity<RuntimeEnvironmentContainerEntity>().HasIndex(entity => new { entity.RuntimeEnvironmentId, entity.Name }).IsUnique();
        modelBuilder.Entity<RuntimeEnvironmentContainerEntity>().HasIndex(entity => entity.RuntimeEnvironmentId);
        modelBuilder.Entity<PromptTemplateEntity>().HasIndex(entity => new { entity.AgentEnvironment, entity.Code }).IsUnique();
        modelBuilder.Entity<EntityAssociationEntity>()
            .HasIndex(entity => new
            {
                entity.SourceEntityId,
                entity.TargetEntityId,
                entity.AssociationType
            })
            .IsUnique();
        modelBuilder.Entity<UserRoleEntity>().HasIndex(entity => new { entity.UserId, entity.RoleId }).IsUnique();
        modelBuilder.Entity<RoleMenuEntity>().HasIndex(entity => new { entity.RoleId, entity.MenuId }).IsUnique();
        modelBuilder.Entity<RolePermissionEntity>().HasIndex(entity => new { entity.RoleId, entity.PermissionId }).IsUnique();
        modelBuilder.Entity<SprintProjectEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<SprintProjectMemberEntity>().HasIndex(entity => new { entity.ProjectId, entity.UserId, entity.Role }).IsUnique();
        modelBuilder.Entity<SprintProjectEndpointEntity>().HasIndex(entity => new { entity.ProjectId, entity.Code }).IsUnique();
        modelBuilder.Entity<SprintFeatureModuleEntity>().HasIndex(entity => new { entity.ProjectId, entity.EndpointId, entity.Code }).IsUnique();
        modelBuilder.Entity<SprintRequirementEntity>().HasIndex(entity => new { entity.ProjectId, entity.Status });
        modelBuilder.Entity<SprintSkillEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<SprintFeatureSuggestionEntity>().HasIndex(entity => new { entity.ProjectId, entity.ModuleId, entity.RequirementId });
        modelBuilder.Entity<SprintRequirementFeedbackEntity>().HasIndex(entity => new { entity.RequirementId, entity.Status });
        modelBuilder.Entity<SprintRequirementReviewEntity>().HasIndex(entity => new { entity.RequirementId, entity.ReviewerId }).IsUnique();
        modelBuilder.Entity<SprintDevelopmentTaskEntity>().HasIndex(entity => new { entity.ProjectId, entity.RequirementId, entity.AssigneeId });
        modelBuilder.Entity<SprintBugEntity>().HasIndex(entity => new { entity.ProjectId, entity.RequirementId });
        modelBuilder.Entity<SprintTaskLeaseEntity>().HasIndex(entity => new { entity.ProjectId, entity.OwnerId, entity.Status });
        modelBuilder.Entity<SprintTaskLeaseEntity>().HasIndex(entity => entity.LeaseToken).IsUnique();
        modelBuilder.Entity<SprintTaskLeaseEntity>().HasIndex(entity => entity.ActiveTargetKey).IsUnique();
        modelBuilder.Entity<TestPlanEntity>().HasIndex(entity => new { entity.ProjectId, entity.RequirementId });
        modelBuilder.Entity<TestExecutionEntity>().HasIndex(entity => entity.TestPlanId);
    }
}
