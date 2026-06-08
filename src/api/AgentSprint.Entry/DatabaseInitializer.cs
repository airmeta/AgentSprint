using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Repository.DbContexts;
using AgentSprint.Service.Security;

using Microsoft.EntityFrameworkCore;

namespace AgentSprint.Entry;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// zh-cn: 创建数据库初始化托管服务，启动时根据配置决定是否初始化数据库结构和默认管理员数据。
    /// en-us: Creates the database initialization hosted service and defers schema and default administrator setup to startup-time configuration.
    /// </summary>
    /// <param name="configuration">
    /// zh-cn: 应用配置，用于读取 Database:AutoInitialize 开关。
    /// en-us: Application configuration used to read the Database:AutoInitialize switch.
    /// </param>
    /// <param name="serviceProvider">
    /// zh-cn: 根服务容器，用于创建作用域并解析数据库上下文。
    /// en-us: Root service provider used to create a scope and resolve the database context.
    /// </param>
    public DatabaseInitializer(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// zh-cn: 应用启动时执行数据库初始化。禁用 AutoInitialize 时不产生副作用；启用后会创建缺失数据库、补齐测试模块表，并在管理员不存在时写入默认管理员、角色、菜单和权限。
    /// en-us: Runs database initialization during application startup. When AutoInitialize is disabled it has no side effects; when enabled it creates the missing database, ensures test-module tables exist, and seeds the default administrator, role, menus, and permission only when the administrator is absent.
    /// </summary>
    /// <param name="cancellationToken">
    /// zh-cn: 启动取消令牌，会传递给 EF Core 数据库操作。
    /// en-us: Startup cancellation token propagated to EF Core database operations.
    /// </param>
    /// <returns>
    /// zh-cn: 表示初始化生命周期完成的任务。
    /// en-us: A task representing completion of the initialization lifecycle step.
    /// </returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!AutoInitialize())
        {
            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DefaultDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureAgentTokenTablesAsync(dbContext, cancellationToken);
        await EnsureSystemConfigurationTablesAsync(dbContext, cancellationToken);
        await EnsureSecurityEvolutionTablesAsync(dbContext, cancellationToken);
        await EnsureAgileMvpTablesAsync(dbContext, cancellationToken);
        await EnsureTestTablesAsync(dbContext, cancellationToken);
        await SeedAdminAsync(dbContext, cancellationToken);
        await SeedDashboardMenuAsync(dbContext, cancellationToken);
        await SeedMvpMenuAsync(dbContext, cancellationToken);
        await SeedSecurityEvolutionAsync(dbContext, cancellationToken);
    }

    /// <summary>
    /// zh-cn: 托管服务停止时无需回收外部资源，因此返回已完成任务。
    /// en-us: Returns a completed task because this hosted service does not hold external resources during shutdown.
    /// </summary>
    /// <param name="cancellationToken">
    /// zh-cn: 停止取消令牌；当前实现不需要使用。
    /// en-us: Shutdown cancellation token; it is not required by the current implementation.
    /// </param>
    /// <returns>
    /// zh-cn: 已完成任务。
    /// en-us: A completed task.
    /// </returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static async Task EnsureAgentTokenTablesAsync(DefaultDbContext dbContext, CancellationToken cancellationToken)
    {
        const string tokenSql = """
            CREATE TABLE IF NOT EXISTS sys_agent_token (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              TokenHash varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              TokenValue varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              TokenPrefix varchar(8) CHARACTER SET utf8mb4 NOT NULL,
              TokenSuffix varchar(8) CHARACTER SET utf8mb4 NOT NULL,
              OwnerUserId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NULL,
              ExpiresAt datetime(6) NOT NULL,
              LastUsedAt datetime(6) NULL,
              RevokedAt datetime(6) NULL,
              RevokedBy varchar(64) CHARACTER SET utf8mb4 NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              CONSTRAINT PK_sys_agent_token PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_agent_token_TokenHash (TokenHash),
              INDEX IX_sys_agent_token_OwnerUserId_Status (OwnerUserId, Status),
              INDEX IX_sys_agent_token_ProjectId (ProjectId)
            ) CHARACTER SET=utf8mb4;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(tokenSql, cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sys_agent_token",
            "TokenValue",
            "ALTER TABLE sys_agent_token ADD COLUMN TokenValue varchar(128) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';",
            cancellationToken);
    }

    private static async Task EnsureSystemConfigurationTablesAsync(DefaultDbContext dbContext, CancellationToken cancellationToken)
    {
        const string configurationSql = """
            CREATE TABLE IF NOT EXISTS sys_configuration (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              `Key` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Value` varchar(2048) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_configuration_Key (`Key`)
            ) CHARACTER SET=utf8mb4;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(configurationSql, cancellationToken);
    }

    private static async Task EnsureTestTablesAsync(DefaultDbContext dbContext, CancellationToken cancellationToken)
    {
        const string testPlanSql = """
            CREATE TABLE IF NOT EXISTS test_plan (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              BugId varchar(64) CHARACTER SET utf8mb4 NULL,
              TesterId varchar(64) CHARACTER SET utf8mb4 NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Environment varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              TestUrl varchar(512) CHARACTER SET utf8mb4 NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              StartedAt datetime(6) NULL,
              CompletedAt datetime(6) NULL,
              Summary varchar(1024) CHARACTER SET utf8mb4 NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_test_plan_ProjectId_RequirementId (ProjectId, RequirementId)
            ) CHARACTER SET=utf8mb4;
            """;

        const string testExecutionSql = """
            CREATE TABLE IF NOT EXISTS test_execution (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              TestPlanId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              BugId varchar(64) CHARACTER SET utf8mb4 NULL,
              TesterId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Result varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              ActualResult varchar(2048) CHARACTER SET utf8mb4 NULL,
              Evidence varchar(2048) CHARACTER SET utf8mb4 NULL,
              CreatedBugId varchar(64) CHARACTER SET utf8mb4 NULL,
              ExecutedAt datetime(6) NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_test_execution_TestPlanId (TestPlanId)
            ) CHARACTER SET=utf8mb4;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(testPlanSql, cancellationToken);
        await EnsureTestPlanColumnsAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(testExecutionSql, cancellationToken);
    }

    private static async Task EnsureSecurityEvolutionTablesAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const string userGroupSql = """
            CREATE TABLE IF NOT EXISTS sys_user_group (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_user_group_Code (Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string roleGroupSql = """
            CREATE TABLE IF NOT EXISTS sys_role_group (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_role_group_Code (Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string departmentSql = """
            CREATE TABLE IF NOT EXISTS sys_department (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ParentId varchar(64) CHARACTER SET utf8mb4 NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Sort int NOT NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_department_Code (Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string assignmentSql = """
            CREATE TABLE IF NOT EXISTS assignment (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_assignment_Code (Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string dictionaryTypeSql = """
            CREATE TABLE IF NOT EXISTS sys_dictionary_type (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Sort int NOT NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_dictionary_type_Code (Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string dictionaryItemSql = """
            CREATE TABLE IF NOT EXISTS sys_dictionary_item (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              DictionaryTypeId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Sort int NOT NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_dictionary_item_Type_Code (DictionaryTypeId, Code),
              INDEX IX_sys_dictionary_item_DictionaryTypeId (DictionaryTypeId)
            ) CHARACTER SET=utf8mb4;
            """;

        const string associationSql = """
            CREATE TABLE IF NOT EXISTS sys_entity_association (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              SourceEntityId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              TargetEntityId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              AssociationType varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_entity_association_Source_Target_Type (
                SourceEntityId,
                TargetEntityId,
                AssociationType
              )
            ) CHARACTER SET=utf8mb4;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(userGroupSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(roleGroupSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(departmentSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(assignmentSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(dictionaryTypeSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(dictionaryItemSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(associationSql, cancellationToken);
        await EnsureRuntimeManagementTablesAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureRuntimeManagementTablesAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const string runtimeEnvironmentSql = """
            CREATE TABLE IF NOT EXISTS sys_runtime_environment (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NULL,
              EndpointId varchar(64) CHARACTER SET utf8mb4 NULL,
              ModuleId varchar(64) CHARACTER SET utf8mb4 NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              EnvironmentType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(1024) CHARACTER SET utf8mb4 NULL,
              FrontendUrl varchar(512) CHARACTER SET utf8mb4 NULL,
              ApiBaseUrl varchar(512) CHARACTER SET utf8mb4 NULL,
              FrontendProxyApiUrl varchar(512) CHARACTER SET utf8mb4 NULL,
              McpEndpoint varchar(512) CHARACTER SET utf8mb4 NULL,
              DeployRoot varchar(512) CHARACTER SET utf8mb4 NULL,
              DockerDirectory varchar(512) CHARACTER SET utf8mb4 NULL,
              RemotePackagePath varchar(512) CHARACTER SET utf8mb4 NULL,
              ComposeFilePath varchar(512) CHARACTER SET utf8mb4 NULL,
              LocalPackagePaths varchar(2048) CHARACTER SET utf8mb4 NULL,
              Sort int NOT NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_runtime_environment_ProjectId_Code (ProjectId, Code),
              INDEX IX_sys_runtime_environment_Project_Endpoint_Module (ProjectId, EndpointId, ModuleId)
            ) CHARACTER SET=utf8mb4;
            """;

        const string runtimeContainerSql = """
            CREATE TABLE IF NOT EXISTS sys_runtime_environment_container (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              RuntimeEnvironmentId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              HostPort int NOT NULL,
              ContainerPort int NOT NULL,
              Protocol varchar(16) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Sort int NOT NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_runtime_environment_container_Environment_Name (RuntimeEnvironmentId, Name),
              INDEX IX_sys_runtime_environment_container_RuntimeEnvironmentId (RuntimeEnvironmentId)
            ) CHARACTER SET=utf8mb4;
            """;

        const string promptTemplateSql = """
            CREATE TABLE IF NOT EXISTS sys_prompt_template (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              AgentEnvironment varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Content varchar(8192) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Sort int NOT NULL,
              Status int NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sys_prompt_template_Environment_Code (AgentEnvironment, Code)
            ) CHARACTER SET=utf8mb4;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(runtimeEnvironmentSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(runtimeContainerSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(promptTemplateSql, cancellationToken);
    }

    private static async Task EnsureAgileMvpTablesAsync(DefaultDbContext dbContext, CancellationToken cancellationToken)
    {
        const string projectSql = """
            CREATE TABLE IF NOT EXISTS sprint_project (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              RepositoryUrl varchar(512) CHARACTER SET utf8mb4 NULL,
              TestEnvironmentUrl varchar(512) CHARACTER SET utf8mb4 NULL,
              TestEnvironmentId varchar(64) CHARACTER SET utf8mb4 NULL,
              Description varchar(2048) CHARACTER SET utf8mb4 NULL,
              FrontendTechStack varchar(512) CHARACTER SET utf8mb4 NULL,
              BackendTechStack varchar(512) CHARACTER SET utf8mb4 NULL,
              ProjectManagerId varchar(64) CHARACTER SET utf8mb4 NULL,
              ProductManagerIds varchar(512) CHARACTER SET utf8mb4 NULL,
              DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              ArchitectId varchar(64) CHARACTER SET utf8mb4 NULL,
              SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sprint_project_Code (Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string skillSql = """
            CREATE TABLE IF NOT EXISTS sprint_skill (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Content varchar(8192) CHARACTER SET utf8mb4 NOT NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sprint_skill_Code (Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string endpointSql = """
            CREATE TABLE IF NOT EXISTS sprint_project_endpoint (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Type varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              OwnerId varchar(64) CHARACTER SET utf8mb4 NULL,
              DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              Sort int NOT NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sprint_project_endpoint_ProjectId_Code (ProjectId, Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string moduleSql = """
            CREATE TABLE IF NOT EXISTS sprint_feature_module (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              EndpointId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(1024) CHARACTER SET utf8mb4 NULL,
              OwnerId varchar(64) CHARACTER SET utf8mb4 NULL,
              DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              Sort int NOT NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sprint_feature_module_ProjectId_EndpointId_Code (ProjectId, EndpointId, Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string requirementSql = """
            CREATE TABLE IF NOT EXISTS sprint_requirement (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              EndpointId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              ModuleId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Title varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(2048) CHARACTER SET utf8mb4 NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              Priority int NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Stakeholders varchar(512) CHARACTER SET utf8mb4 NULL,
              ReviewedBy varchar(64) CHARACTER SET utf8mb4 NULL,
              DeveloperId varchar(64) CHARACTER SET utf8mb4 NULL,
              TestUrl varchar(512) CHARACTER SET utf8mb4 NULL,
              ApprovedAt datetime(6) NULL,
              SubmittedAt datetime(6) NULL,
              DevelopmentCompletedAt datetime(6) NULL,
              TestedAt datetime(6) NULL,
              ClosedAt datetime(6) NULL,
              VoidedAt datetime(6) NULL,
              SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_sprint_requirement_ProjectId_Status (ProjectId, Status)
            ) CHARACTER SET=utf8mb4;
            """;

        const string suggestionSql = """
            CREATE TABLE IF NOT EXISTS sprint_feature_suggestion (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              EndpointId varchar(64) CHARACTER SET utf8mb4 NULL,
              ModuleId varchar(64) CHARACTER SET utf8mb4 NULL,
              RequirementId varchar(64) CHARACTER SET utf8mb4 NULL,
              Content varchar(2048) CHARACTER SET utf8mb4 NOT NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              ConvertedRequirementId varchar(64) CHARACTER SET utf8mb4 NULL,
              ConvertedAt datetime(6) NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_sprint_feature_suggestion_ProjectId_ModuleId_RequirementId (ProjectId, ModuleId, RequirementId)
            ) CHARACTER SET=utf8mb4;
            """;

        const string projectMemberSql = """
            CREATE TABLE IF NOT EXISTS sprint_project_member (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              UserId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Role varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sprint_project_member_ProjectId_UserId_Role (ProjectId, UserId, Role)
            ) CHARACTER SET=utf8mb4;
            """;

        const string reviewSql = """
            CREATE TABLE IF NOT EXISTS sprint_requirement_review (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              ReviewerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              Comment varchar(512) CHARACTER SET utf8mb4 NULL,
              ReviewedAt datetime(6) NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sprint_requirement_review_RequirementId_ReviewerId (RequirementId, ReviewerId)
            ) CHARACTER SET=utf8mb4;
            """;

        const string taskSql = """
            CREATE TABLE IF NOT EXISTS sprint_development_task (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              DevelopmentTaskId varchar(64) CHARACTER SET utf8mb4 NULL,
              Title varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(2048) CHARACTER SET utf8mb4 NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              Priority int NOT NULL,
              AssigneeId varchar(64) CHARACTER SET utf8mb4 NULL,
              AssignedBy varchar(64) CHARACTER SET utf8mb4 NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Prompt varchar(8192) CHARACTER SET utf8mb4 NULL,
              AssignedAt datetime(6) NULL,
              StartedAt datetime(6) NULL,
              CompletedAt datetime(6) NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_sprint_development_task_ProjectId_RequirementId_AssigneeId (ProjectId, RequirementId, AssigneeId)
            ) CHARACTER SET=utf8mb4;
            """;

        const string bugSql = """
            CREATE TABLE IF NOT EXISTS sprint_bug (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              TestPlanId varchar(64) CHARACTER SET utf8mb4 NULL,
              TestExecutionId varchar(64) CHARACTER SET utf8mb4 NULL,
              Title varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(2048) CHARACTER SET utf8mb4 NULL,
              Environment varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              Severity varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'major',
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              DeveloperId varchar(64) CHARACTER SET utf8mb4 NULL,
              FixedAt datetime(6) NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_sprint_bug_ProjectId_RequirementId (ProjectId, RequirementId)
            ) CHARACTER SET=utf8mb4;
            """;

        const string leaseSql = """
            CREATE TABLE IF NOT EXISTS sprint_task_lease (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              TargetType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              TargetId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              ActiveTargetKey varchar(128) CHARACTER SET utf8mb4 NULL,
              OwnerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              OwnerDevice varchar(128) CHARACTER SET utf8mb4 NULL,
              LeaseToken varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              ExpiresAt datetime(6) NOT NULL,
              CompletedAt datetime(6) NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_sprint_task_lease_LeaseToken (LeaseToken),
              UNIQUE INDEX IX_sprint_task_lease_ActiveTargetKey (ActiveTargetKey),
              INDEX IX_sprint_task_lease_ProjectId_OwnerId_Status (ProjectId, OwnerId, Status)
            ) CHARACTER SET=utf8mb4;
            """;

        const string feedbackSql = """
            CREATE TABLE IF NOT EXISTS sprint_requirement_feedback (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Title varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Content varchar(2048) CHARACTER SET utf8mb4 NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              ConvertedRequirementId varchar(64) CHARACTER SET utf8mb4 NULL,
              ConvertedAt datetime(6) NULL,
              ClosedAt datetime(6) NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_sprint_requirement_feedback_RequirementId_Status (RequirementId, Status)
            ) CHARACTER SET=utf8mb4;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(projectSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(skillSql, cancellationToken);
        await EnsureProjectColumnsAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(endpointSql, cancellationToken);
        await EnsureEndpointColumnsAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(moduleSql, cancellationToken);
        await EnsureModuleColumnsAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(projectMemberSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(requirementSql, cancellationToken);
        await EnsureRequirementColumnsAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(suggestionSql, cancellationToken);
        await EnsureSuggestionColumnsAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(reviewSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(taskSql, cancellationToken);
        await EnsureTaskColumnsAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(bugSql, cancellationToken);
        await EnsureBugColumnsAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(leaseSql, cancellationToken);
        await EnsureLeaseColumnsAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(feedbackSql, cancellationToken);
        await EnsureFeedbackColumnsAsync(dbContext, cancellationToken);
        await BackfillProjectMembersAsync(dbContext, cancellationToken);
    }

    private static async Task BackfillProjectMembersAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var memberKeys = (await dbContext.SprintProjectMembers
                .Select(entity => new { entity.ProjectId, entity.UserId, entity.Role })
                .ToListAsync(cancellationToken))
            .Select(entity => $"{entity.ProjectId}\u001F{entity.UserId}\u001F{entity.Role}")
            .ToHashSet(StringComparer.Ordinal);

        var projects = await dbContext.SprintProjects
            .Where(entity => entity.IsDelete == 0 && entity.CreatedBy != string.Empty)
            .ToListAsync(cancellationToken);
        foreach (var project in projects)
        {
            await EnsureProjectMemberAsync(
                dbContext,
                memberKeys,
                project.Id,
                project.CreatedBy,
                "project_manager",
                cancellationToken);
            await EnsureProjectMemberAsync(
                dbContext,
                memberKeys,
                project.Id,
                project.ProjectManagerId,
                "project_manager",
                cancellationToken);
            await EnsureProjectMemberAsync(
                dbContext,
                memberKeys,
                project.Id,
                project.ArchitectId,
                "architect",
                cancellationToken);
            foreach (var productManagerId in SplitIds(project.ProductManagerIds))
            {
                await EnsureProjectMemberAsync(
                    dbContext,
                    memberKeys,
                    project.Id,
                    productManagerId,
                    "product",
                    cancellationToken);
            }

            foreach (var developerId in SplitIds(project.DeveloperIds))
            {
                await EnsureProjectMemberAsync(
                    dbContext,
                    memberKeys,
                    project.Id,
                    developerId,
                    "developer",
                    cancellationToken);
            }

            foreach (var testerId in SplitIds(project.TesterIds))
            {
                await EnsureProjectMemberAsync(
                    dbContext,
                    memberKeys,
                    project.Id,
                    testerId,
                    "tester",
                    cancellationToken);
            }
        }

        var requirements = await dbContext.SprintRequirements
            .Where(entity => entity.IsDelete == 0)
            .ToListAsync(cancellationToken);
        foreach (var requirement in requirements)
        {
            await EnsureProjectMemberAsync(
                dbContext,
                memberKeys,
                requirement.ProjectId,
                requirement.CreatedBy,
                "product",
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(requirement.DeveloperId))
            {
                await EnsureProjectMemberAsync(
                    dbContext,
                    memberKeys,
                    requirement.ProjectId,
                    requirement.DeveloperId,
                    "developer",
                    cancellationToken);
            }
        }

        var reviews = await dbContext.SprintRequirementReviews
            .Where(entity => entity.IsDelete == 0)
            .ToListAsync(cancellationToken);
        foreach (var review in reviews)
        {
            await EnsureProjectMemberAsync(
                dbContext,
                memberKeys,
                review.ProjectId,
                review.ReviewerId,
                "reviewer",
                cancellationToken);
        }

        var tasks = await dbContext.SprintDevelopmentTasks
            .Where(entity => entity.IsDelete == 0 && entity.AssigneeId != null)
            .ToListAsync(cancellationToken);
        foreach (var task in tasks)
        {
            await EnsureProjectMemberAsync(
                dbContext,
                memberKeys,
                task.ProjectId,
                task.AssigneeId!,
                "developer",
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureProjectMemberAsync(
        DefaultDbContext dbContext,
        ISet<string> memberKeys,
        string projectId,
        string? userId,
        string role,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId) ||
            string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        var existing = await dbContext.SprintProjectMembers.FirstOrDefaultAsync(
            entity => entity.ProjectId == projectId &&
                entity.UserId == userId &&
                entity.Role == role,
            cancellationToken);
        if (existing is not null)
        {
            existing.Status = "active";
            return;
        }

        var key = $"{projectId}\u001F{userId}\u001F{role}";
        if (!memberKeys.Add(key))
        {
            return;
        }

        dbContext.SprintProjectMembers.Add(new AgentSprint.Model.Modules.Agile.SprintProjectMemberEntity
        {
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            Status = "active"
        });
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyList<string> SplitIds(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
    }

    private static async Task EnsureProjectColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "Description",
            "ALTER TABLE sprint_project ADD COLUMN Description varchar(2048) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "FrontendTechStack",
            "ALTER TABLE sprint_project ADD COLUMN FrontendTechStack varchar(512) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "BackendTechStack",
            "ALTER TABLE sprint_project ADD COLUMN BackendTechStack varchar(512) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "ProjectManagerId",
            "ALTER TABLE sprint_project ADD COLUMN ProjectManagerId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "ProductManagerIds",
            "ALTER TABLE sprint_project ADD COLUMN ProductManagerIds varchar(512) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "DeveloperIds",
            "ALTER TABLE sprint_project ADD COLUMN DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "TesterIds",
            "ALTER TABLE sprint_project ADD COLUMN TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "ArchitectId",
            "ALTER TABLE sprint_project ADD COLUMN ArchitectId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "TestEnvironmentId",
            "ALTER TABLE sprint_project ADD COLUMN TestEnvironmentId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "SkillIds",
            "ALTER TABLE sprint_project ADD COLUMN SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
    }

    private static async Task EnsureEndpointColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sprint_project_endpoint",
            "OwnerId",
            "ALTER TABLE sprint_project_endpoint ADD COLUMN OwnerId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project_endpoint",
            "DeveloperIds",
            "ALTER TABLE sprint_project_endpoint ADD COLUMN DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project_endpoint",
            "TesterIds",
            "ALTER TABLE sprint_project_endpoint ADD COLUMN TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project_endpoint",
            "SkillIds",
            "ALTER TABLE sprint_project_endpoint ADD COLUMN SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
    }

    private static async Task EnsureModuleColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sprint_feature_module",
            "OwnerId",
            "ALTER TABLE sprint_feature_module ADD COLUMN OwnerId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_feature_module",
            "DeveloperIds",
            "ALTER TABLE sprint_feature_module ADD COLUMN DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_feature_module",
            "TesterIds",
            "ALTER TABLE sprint_feature_module ADD COLUMN TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
    }

    private static async Task EnsureRequirementColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sprint_requirement",
            "Stakeholders",
            "ALTER TABLE sprint_requirement ADD COLUMN Stakeholders varchar(512) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_requirement",
            "EndpointId",
            "ALTER TABLE sprint_requirement ADD COLUMN EndpointId varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_requirement",
            "ModuleId",
            "ALTER TABLE sprint_requirement ADD COLUMN ModuleId varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_requirement",
            "SubmittedAt",
            "ALTER TABLE sprint_requirement ADD COLUMN SubmittedAt datetime(6) NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_requirement",
            "VoidedAt",
            "ALTER TABLE sprint_requirement ADD COLUMN VoidedAt datetime(6) NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_requirement",
            "SourceRequirementId",
            "ALTER TABLE sprint_requirement ADD COLUMN SourceRequirementId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_requirement",
            "SourceFeedbackId",
            "ALTER TABLE sprint_requirement ADD COLUMN SourceFeedbackId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_requirement",
            "SkillIds",
            "ALTER TABLE sprint_requirement ADD COLUMN SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
    }

    private static async Task EnsureTaskColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sprint_development_task",
            "AssignedBy",
            "ALTER TABLE sprint_development_task ADD COLUMN AssignedBy varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE sprint_development_task MODIFY COLUMN Prompt varchar(8192) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
    }

    private static async Task EnsureSuggestionColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sprint_feature_suggestion",
            "ConvertedRequirementId",
            "ALTER TABLE sprint_feature_suggestion ADD COLUMN ConvertedRequirementId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_feature_suggestion",
            "ConvertedAt",
            "ALTER TABLE sprint_feature_suggestion ADD COLUMN ConvertedAt datetime(6) NULL;",
            cancellationToken);
    }

    private static async Task EnsureFeedbackColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sprint_requirement_feedback",
            "DevelopmentTaskId",
            "ALTER TABLE sprint_requirement_feedback ADD COLUMN DevelopmentTaskId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
    }

    private static async Task EnsureTestPlanColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "test_plan",
            "TesterId",
            "ALTER TABLE test_plan ADD COLUMN TesterId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
    }

    private static async Task EnsureBugColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sprint_bug",
            "Severity",
            "ALTER TABLE sprint_bug ADD COLUMN Severity varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'major';",
            cancellationToken);
    }

    private static async Task EnsureLeaseColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sprint_task_lease",
            "ActiveTargetKey",
            "ALTER TABLE sprint_task_lease ADD COLUMN ActiveTargetKey varchar(128) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE sprint_task_lease
            SET Status = 'released',
                CompletedAt = COALESCE(CompletedAt, UTC_TIMESTAMP(6)),
                ActiveTargetKey = NULL
            WHERE Status = 'active'
              AND ExpiresAt <= UTC_TIMESTAMP(6);
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE sprint_task_lease lease
            JOIN (
                SELECT TargetType, TargetId, MIN(Id) AS KeepId
                FROM sprint_task_lease
                WHERE Status = 'active'
                  AND ExpiresAt > UTC_TIMESTAMP(6)
                  AND IsDelete = 0
                GROUP BY TargetType, TargetId
                HAVING COUNT(*) > 1
            ) duplicate_target
              ON duplicate_target.TargetType = lease.TargetType
             AND duplicate_target.TargetId = lease.TargetId
            SET lease.Status = 'released',
                lease.CompletedAt = COALESCE(lease.CompletedAt, UTC_TIMESTAMP(6)),
                lease.ActiveTargetKey = NULL
            WHERE lease.Id <> duplicate_target.KeepId;
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE sprint_task_lease
            SET ActiveTargetKey = NULL
            WHERE Status <> 'active'
               OR ExpiresAt <= UTC_TIMESTAMP(6)
               OR IsDelete <> 0;
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE sprint_task_lease
            SET ActiveTargetKey = CONCAT(TargetType, ':', TargetId)
            WHERE Status = 'active'
              AND ExpiresAt > UTC_TIMESTAMP(6)
              AND IsDelete = 0;
            """,
            cancellationToken);
        await EnsureIndexAsync(
            dbContext,
            "sprint_task_lease",
            "IX_sprint_task_lease_ActiveTargetKey",
            "CREATE UNIQUE INDEX IX_sprint_task_lease_ActiveTargetKey ON sprint_task_lease (ActiveTargetKey);",
            cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        DefaultDbContext dbContext,
        string tableName,
        string columnName,
        string alterSql,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = @tableName
              AND column_name = @columnName;
            """;

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@tableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);

        var columnParameter = command.CreateParameter();
        columnParameter.ParameterName = "@columnName";
        columnParameter.Value = columnName;
        command.Parameters.Add(columnParameter);

        var existingCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (existingCount == 0)
        {
            await dbContext.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
        }
    }

    private static async Task EnsureIndexAsync(
        DefaultDbContext dbContext,
        string tableName,
        string indexName,
        string createSql,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND table_name = @tableName
              AND index_name = @indexName;
            """;

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@tableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);

        var indexParameter = command.CreateParameter();
        indexParameter.ParameterName = "@indexName";
        indexParameter.Value = indexName;
        command.Parameters.Add(indexParameter);

        var existingCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (existingCount == 0)
        {
            await dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
        }
    }


    private static async Task SeedAdminAsync(DefaultDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(entity => entity.Username == "admin", cancellationToken))
        {
            return;
        }

        var user = new UserEntity
        {
            Username = "admin",
            DisplayName = "Administrator",
            PasswordHash = PasswordHasher.Hash("123456"),
            Avatar = "https://unpkg.com/@vbenjs/static-source@0.1.7/source/avatar-v1.webp"
        };
        var role = new RoleEntity
        {
            Code = "super",
            Name = "Super Administrator"
        };
        var workspaceMenu = new MenuEntity
        {
            Path = "/dashboard/workspace",
            Name = "Workspace",
            Component = "/dashboard/workspace/index",
            Icon = "lucide:panel-top",
            Sort = 0
        };
        var permission = new PermissionEntity
        {
            Code = "system:all",
            Name = "All system permissions"
        };

        dbContext.Users.Add(user);
        dbContext.Roles.Add(role);
        dbContext.Menus.Add(workspaceMenu);
        dbContext.Permissions.Add(permission);
        dbContext.UserRoles.Add(new UserRoleEntity { UserId = user.Id, RoleId = role.Id });
        dbContext.RoleMenus.AddRange(
            new RoleMenuEntity { RoleId = role.Id, MenuId = workspaceMenu.Id });
        dbContext.RolePermissions.Add(new RolePermissionEntity { RoleId = role.Id, PermissionId = permission.Id });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedMvpMenuAsync(DefaultDbContext dbContext, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.FirstOrDefaultAsync(entity => entity.Code == "super", cancellationToken);
        if (role is null)
        {
            return;
        }

        var parentMenu = await dbContext.Menus.FirstOrDefaultAsync(
            entity => entity.Path == "/sprint",
            cancellationToken);
        if (parentMenu is null)
        {
            parentMenu = new MenuEntity
            {
                Path = "/sprint"
            };
            dbContext.Menus.Add(parentMenu);
        }

        parentMenu.Name = "Sprint";
        parentMenu.Icon = "lucide:kanban";
        parentMenu.Sort = 30;
        parentMenu.Type = 2;
        parentMenu.Status = 0;

        await EnsureRoleMenuAsync(dbContext, role.Id, parentMenu.Id, cancellationToken);
        await EnsureAssociationAsync(
            dbContext,
            role.Id,
            parentMenu.Id,
            SecurityAssociationTypes.RoleMenu,
            cancellationToken);

        foreach (var removedPath in new[]
        {
            "/demos",
            "/vben-admin",
            "/vben-admin/about",
            "/sprint",
            "/sprint/mvp"
        })
        {
            await DisableMenuAsync(dbContext, removedPath, cancellationToken);
        }

        var projectGroup = await EnsureMenuAsync(
            dbContext,
            role.Id,
            string.Empty,
            "/sprint/project",
            "ProjectGroup",
            string.Empty,
            "lucide:folder-kanban",
            10,
            0,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, projectGroup.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var productGroup = await EnsureMenuAsync(
            dbContext,
            role.Id,
            string.Empty,
            "/sprint/product",
            "ProductGroup",
            string.Empty,
            "lucide:list-checks",
            20,
            0,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, productGroup.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var workerGroup = await EnsureMenuAsync(
            dbContext,
            role.Id,
            string.Empty,
            "/sprint/worker",
            "WorkerGroup",
            string.Empty,
            "lucide:workflow",
            30,
            0,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, workerGroup.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var testGroup = await EnsureMenuAsync(
            dbContext,
            role.Id,
            string.Empty,
            "/sprint/test",
            "TestGroup",
            string.Empty,
            "lucide:test-tube-2",
            40,
            0,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, testGroup.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var projectMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            projectGroup.Id,
            "/sprint/projects",
            "SprintProjects",
            "/sprint/projects/index",
            "lucide:folder-kanban",
            10,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, projectMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var multiEndpointsMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            projectGroup.Id,
            "/sprint/multi-endpoints",
            "SprintMultiEndpoints",
            "/sprint/multi-endpoints/index",
            "lucide:layout-dashboard",
            11,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, multiEndpointsMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var skillsMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            projectGroup.Id,
            "/sprint/skills",
            "SprintSkills",
            "/sprint/skills/index",
            "lucide:brain-circuit",
            12,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, skillsMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var projectDetailRoute = await EnsureMenuAsync(
            dbContext,
            role.Id,
            projectGroup.Id,
            "/sprint/projects/detail/:id",
            "SprintProjectDetail",
            "/sprint/projects/detail",
            null,
            13,
            2,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, projectDetailRoute.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var requirementsMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            productGroup.Id,
            "/sprint/requirements",
            "SprintRequirements",
            "/sprint/requirements/index",
            "lucide:list-checks",
            10,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, requirementsMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var requirementDetailMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            productGroup.Id,
            "/sprint/requirements/detail/:id",
            "SprintRequirementDetail",
            "/sprint/requirements/detail",
            null,
            11,
            2,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, requirementDetailMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var reviewsMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            productGroup.Id,
            "/sprint/reviews",
            "SprintRequirementReviews",
            "/sprint/reviews/index",
            "lucide:clipboard-check",
            20,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, reviewsMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var myTasksMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            workerGroup.Id,
            "/sprint/my-tasks",
            "SprintMyTasks",
            "/sprint/my-tasks/index",
            "lucide:user-check",
            10,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, myTasksMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var tasksMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            workerGroup.Id,
            "/sprint/tasks",
            "SprintTasks",
            "/sprint/tasks/index",
            "lucide:layout-list",
            20,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, tasksMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var taskDetailMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            workerGroup.Id,
            "/sprint/tasks/detail/:id",
            "SprintTaskDetail",
            "/sprint/tasks/detail",
            null,
            21,
            2,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, taskDetailMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var testsMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            testGroup.Id,
            "/sprint/tests",
            "SprintTests",
            "/sprint/tests/index",
            "lucide:test-tube-2",
            10,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, testsMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var defectsMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            testGroup.Id,
            "/sprint/defects",
            "SprintDefects",
            "/sprint/defects/index",
            "lucide:bug",
            20,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, defectsMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var defectDetailMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            testGroup.Id,
            "/sprint/defects/detail/:id",
            "SprintDefectDetail",
            "/sprint/defects/detail",
            null,
            21,
            2,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, defectDetailMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDashboardMenuAsync(DefaultDbContext dbContext, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.FirstOrDefaultAsync(entity => entity.Code == "super", cancellationToken);
        if (role is null)
        {
            return;
        }

        await DisableMenuAsync(dbContext, "/dashboard", cancellationToken);
        await DisableMenuAsync(dbContext, "/dashboard/analytics", cancellationToken);

        var workspace = await EnsureMenuAsync(
            dbContext,
            role.Id,
            string.Empty,
            "/dashboard/workspace",
            "Workspace",
            "/dashboard/workspace/index",
            "lucide:panel-top",
            0,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, workspace.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSecurityEvolutionAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await SeedSystemConfigurationsAsync(dbContext, cancellationToken);
        await SeedSystemMenuAsync(dbContext, cancellationToken);
        await SeedRuntimeManagementSamplesAsync(dbContext, cancellationToken);
        await BackfillEntityAssociationsAsync(dbContext, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRuntimeManagementSamplesAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var environment = await dbContext.RuntimeEnvironments.FirstOrDefaultAsync(
            entity => entity.Code == "test" && entity.ProjectId == null,
            cancellationToken);
        if (environment is null)
        {
            environment = new RuntimeEnvironmentEntity
            {
                Code = "test"
            };
            dbContext.RuntimeEnvironments.Add(environment);
        }

        environment.Name = "测试环境";
        environment.EnvironmentType = "test";
        environment.Description = "AgentSprint 默认测试环境，按前端、API、MCP 与部署路径拆分维护。";
        environment.FrontendUrl = "http://192.168.80.101:5999";
        environment.ApiBaseUrl = "http://192.168.80.101:5000";
        environment.FrontendProxyApiUrl = "http://192.168.80.101:5999/api";
        environment.McpEndpoint = "http://192.168.80.101:5010/mcp";
        environment.DeployRoot = "/opt/agentsprint-deploy";
        environment.DockerDirectory = "/opt/agentsprint-deploy/docker";
        environment.RemotePackagePath = "/opt/agentsprint-deploy/agentsprint-docker-deploy.tgz";
        environment.ComposeFilePath = "/opt/agentsprint-deploy/docker/docker-compose.yml";
        environment.LocalPackagePaths = string.Join(
            Environment.NewLine,
            @"F:\AI\AgentSprint\agentsprint-docker-deploy.tar",
            @"F:\AI\AgentSprint\agentsprint-docker-deploy.tgz",
            @"F:\AI\AgentSprint\agentsprint-docker-deploy.tar.zip");
        environment.Sort = 10;
        environment.Status = 1;
        environment.IsDelete = 0;

        await EnsureRuntimeContainerAsync(dbContext, environment.Id, "agentsprint-admin", 5999, 80, 10, cancellationToken);
        await EnsureRuntimeContainerAsync(dbContext, environment.Id, "agentsprint-api", 5000, 5000, 20, cancellationToken);
        await EnsureRuntimeContainerAsync(dbContext, environment.Id, "agentsprint-mcp", 5010, 5010, 30, cancellationToken);

        await EnsurePromptTemplateAsync(
            dbContext,
            "mcp_setup",
            "MCP 接入提示词",
            "请按 AgentSprint MCP 接入说明配置 Codex HTTP MCP，并确保请求头使用 http_headers。",
            "Codex agentsprint MCP 接入配置提示词。",
            10,
            cancellationToken);
        await EnsurePromptTemplateAsync(
            dbContext,
            "task_execution",
            "任务推进提示词",
            "请读取任务上下文、按项目规范实现变更、运行相关测试，并在完成后报告修改点与验证命令。",
            "Codex 任务推进提示词。",
            20,
            cancellationToken);
    }

    private static async Task EnsurePromptTemplateAsync(
        DefaultDbContext dbContext,
        string code,
        string name,
        string content,
        string description,
        int sort,
        CancellationToken cancellationToken)
    {
        var prompt = await dbContext.PromptTemplates.FirstOrDefaultAsync(
            entity => entity.AgentEnvironment == "codex" && entity.Code == code,
            cancellationToken);
        if (prompt is null)
        {
            prompt = new PromptTemplateEntity
            {
                AgentEnvironment = "codex",
                Code = code
            };
            dbContext.PromptTemplates.Add(prompt);
        }

        prompt.Name = name;
        prompt.Content = content;
        prompt.Description = description;
        prompt.Sort = sort;
        prompt.Status = 1;
        prompt.IsDelete = 0;
    }

    private static async Task EnsureRuntimeContainerAsync(
        DefaultDbContext dbContext,
        string runtimeEnvironmentId,
        string name,
        int hostPort,
        int containerPort,
        int sort,
        CancellationToken cancellationToken)
    {
        var container = await dbContext.RuntimeEnvironmentContainers.FirstOrDefaultAsync(
            entity => entity.RuntimeEnvironmentId == runtimeEnvironmentId && entity.Name == name,
            cancellationToken);
        if (container is null)
        {
            container = new RuntimeEnvironmentContainerEntity
            {
                RuntimeEnvironmentId = runtimeEnvironmentId,
                Name = name
            };
            dbContext.RuntimeEnvironmentContainers.Add(container);
        }

        container.HostPort = hostPort;
        container.ContainerPort = containerPort;
        container.Protocol = "tcp";
        container.Sort = sort;
        container.Status = 1;
        container.IsDelete = 0;
    }

    private static async Task SeedSystemConfigurationsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureConfigurationAsync(
            dbContext,
            "Mcp:Endpoint",
            "http://192.168.80.101:5010/mcp",
            "Streamable HTTP MCP service endpoint used in generated task prompts.",
            cancellationToken);
        await EnsureConfigurationAsync(
            dbContext,
            "Sprint:Requirement:SyncTestEnvironmentOnCompletion",
            "false",
            "When true, completing requirement development fills the requirement test URL from the selected project runtime environment.",
            cancellationToken);
        await EnsureDictionaryTypeAsync(
            dbContext,
            "frontend_tech_stack",
            "前端技术栈",
            "Project frontend technology stack options.",
            10,
            cancellationToken,
            ("vue3", "Vue 3", 10),
            ("vite", "Vite", 20),
            ("tdesign", "TDesign", 30),
            ("typescript", "TypeScript", 40));
        await EnsureDictionaryTypeAsync(
            dbContext,
            "backend_tech_stack",
            "后端技术栈",
            "Project backend technology stack options.",
            20,
            cancellationToken,
            ("dotnet", ".NET", 10),
            ("ef-core", "EF Core", 20),
            ("mysql", "MySQL", 30),
            ("mcp", "MCP", 40));
    }

    private static async Task<DictionaryTypeEntity> EnsureDictionaryTypeAsync(
        DefaultDbContext dbContext,
        string code,
        string name,
        string description,
        int sort,
        CancellationToken cancellationToken,
        params (string Code, string Name, int Sort)[] items)
    {
        var type = await dbContext.DictionaryTypes.FirstOrDefaultAsync(
            entity => entity.Code == code,
            cancellationToken);
        if (type is null)
        {
            type = new DictionaryTypeEntity { Code = code };
            dbContext.DictionaryTypes.Add(type);
        }

        type.Name = name;
        type.Description = description;
        type.Sort = sort;
        type.Status = 1;
        type.IsDelete = 0;

        foreach (var item in items)
        {
            var entity = await dbContext.DictionaryItems.FirstOrDefaultAsync(
                dictionaryItem => dictionaryItem.DictionaryTypeId == type.Id && dictionaryItem.Code == item.Code,
                cancellationToken);
            if (entity is null)
            {
                entity = new DictionaryItemEntity
                {
                    DictionaryTypeId = type.Id,
                    Code = item.Code
                };
                dbContext.DictionaryItems.Add(entity);
            }

            entity.Name = item.Name;
            entity.Sort = item.Sort;
            entity.Status = 1;
            entity.IsDelete = 0;
        }

        return type;
    }

    private static async Task SeedSystemMenuAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.FirstOrDefaultAsync(entity => entity.Code == "super", cancellationToken);
        if (role is null)
        {
            return;
        }

        var system = await dbContext.Menus.FirstOrDefaultAsync(entity => entity.Path == "/system", cancellationToken);
        if (system is null)
        {
            system = new MenuEntity { Path = "/system" };
            dbContext.Menus.Add(system);
        }

        system.Name = "System";
        system.Icon = "lucide:settings";
        system.Sort = 90;
        system.Type = 0;
        system.Status = 1;

        await EnsureRoleMenuAsync(dbContext, role.Id, system.Id, cancellationToken);
        await EnsureAssociationAsync(
            dbContext,
            role.Id,
            system.Id,
            SecurityAssociationTypes.RoleMenu,
            cancellationToken);

        await DisableMenuAsync(dbContext, "/system/org", cancellationToken);
        var usersMenu = await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/users", "SystemUsers", "/system/users/index", "lucide:users", 10, cancellationToken);
        var rolesMenu = await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/roles", "SystemRoles", "/system/roles/index", "lucide:shield-check", 20, cancellationToken);
        var menusMenu = await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/menus", "SystemMenus", "/system/menus/index", "lucide:menu", 30, cancellationToken);
        await DisableMenuAsync(dbContext, "/system/permissions", cancellationToken);
        var dictionariesMenu = await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/dictionaries", "SystemDictionaries", "/system/dictionaries/index", "lucide:book-open-text", 40, cancellationToken);
        var configurationsMenu = await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/configurations", "SystemConfigurations", "/system/configurations/index", "lucide:sliders-horizontal", 45, cancellationToken);
        await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/departments", "SystemDepartments", "/system/departments/index", "lucide:network", 50, cancellationToken);
        await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/assignments", "SystemAssignments", "/system/assignments/index", "lucide:briefcase-business", 60, cancellationToken);
        await DisableMenuAsync(dbContext, "/system/runtime-environments", cancellationToken);
        await DisableMenuAsync(dbContext, "/system/prompt-templates", cancellationToken);

        var globalConfig = await dbContext.Menus.FirstOrDefaultAsync(entity => entity.Path == "/global-config", cancellationToken);
        if (globalConfig is null)
        {
            globalConfig = new MenuEntity { Path = "/global-config" };
            dbContext.Menus.Add(globalConfig);
        }

        globalConfig.Name = "GlobalConfig";
        globalConfig.Icon = "lucide:sliders-horizontal";
        globalConfig.Sort = 92;
        globalConfig.Type = 0;
        globalConfig.Status = 1;

        await EnsureRoleMenuAsync(dbContext, role.Id, globalConfig.Id, cancellationToken);
        await EnsureAssociationAsync(
            dbContext,
            role.Id,
            globalConfig.Id,
            SecurityAssociationTypes.RoleMenu,
            cancellationToken);

        var runtimeEnvironmentsMenu = await EnsureSystemMenuAsync(dbContext, role.Id, globalConfig.Id, "/global-config/environments", "GlobalConfigEnvironments", "/system/runtime-environments/index", "lucide:server-cog", 10, cancellationToken);
        var promptTemplatesMenu = await EnsureSystemMenuAsync(dbContext, role.Id, globalConfig.Id, "/global-config/prompt-templates", "GlobalConfigPromptTemplates", "/system/prompt-templates/index", "lucide:message-square-code", 20, cancellationToken);

        var security = await dbContext.Menus.FirstOrDefaultAsync(entity => entity.Path == "/security", cancellationToken);
        if (security is null)
        {
            security = new MenuEntity { Path = "/security" };
            dbContext.Menus.Add(security);
        }

        security.Name = "Security";
        security.Icon = "lucide:shield-check";
        security.Sort = 95;
        security.Type = 0;
        security.Status = 1;

        await EnsureRoleMenuAsync(dbContext, role.Id, security.Id, cancellationToken);
        await EnsureAssociationAsync(
            dbContext,
            role.Id,
            security.Id,
            SecurityAssociationTypes.RoleMenu,
            cancellationToken);

        var agentTokensMenu = await EnsureSystemMenuAsync(dbContext, role.Id, security.Id, "/system/agent-tokens", "SystemAgentTokens", "/system/agent-tokens/index", "lucide:key-square", 10, cancellationToken);

        await EnsurePermissionAsync(dbContext, role.Id, "System:Manage", "System management", system.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "System:User:Manage", "User management", usersMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "System:Role:Manage", "Role management", rolesMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "System:Menu:Manage", "Menu management", menusMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "System:Permission:Manage", "Button permission management", menusMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "System:Dictionary:Manage", "Dictionary management", dictionariesMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "System:RuntimeEnvironment:Manage", "Runtime environment management", runtimeEnvironmentsMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "System:PromptTemplate:Manage", "Prompt template management", promptTemplatesMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "System:Configuration:Manage", "System configuration management", configurationsMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "Security:AgentToken:Manage", "Agent token management", agentTokensMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "System:Organization:Manage", "Organization management", system.Id, cancellationToken);
    }

    private static async Task<MenuEntity> EnsureSystemMenuAsync(
        DefaultDbContext dbContext,
        string roleId,
        string parentId,
        string path,
        string name,
        string component,
        string icon,
        int sort,
        CancellationToken cancellationToken)
    {
        var menu = await EnsureMenuAsync(
            dbContext,
            roleId,
            parentId,
            path,
            name,
            component,
            icon,
            sort,
            1,
            cancellationToken);
        await EnsureAssociationAsync(
            dbContext,
            roleId,
            menu.Id,
            SecurityAssociationTypes.RoleMenu,
            cancellationToken);
        return menu;
    }

    private static async Task<PermissionEntity> EnsurePermissionAsync(
        DefaultDbContext dbContext,
        string roleId,
        string code,
        string name,
        string? menuId,
        CancellationToken cancellationToken)
    {
        var permission = await dbContext.Permissions.FirstOrDefaultAsync(entity => entity.Code == code, cancellationToken);
        if (permission is null)
        {
            permission = new PermissionEntity { Code = code };
            dbContext.Permissions.Add(permission);
        }

        permission.Name = name;
        permission.MenuId = menuId;

        if (!await dbContext.RolePermissions.AnyAsync(
            entity => entity.RoleId == roleId && entity.PermissionId == permission.Id,
            cancellationToken))
        {
            dbContext.RolePermissions.Add(new RolePermissionEntity { RoleId = roleId, PermissionId = permission.Id });
        }

        await EnsureAssociationAsync(
            dbContext,
            roleId,
            permission.Id,
            SecurityAssociationTypes.RolePermission,
            cancellationToken);

        return permission;
    }

    private static async Task<SystemConfigurationEntity> EnsureConfigurationAsync(
        DefaultDbContext dbContext,
        string key,
        string value,
        string description,
        CancellationToken cancellationToken)
    {
        var configuration = await dbContext.SystemConfigurations.FirstOrDefaultAsync(
            entity => entity.Key == key,
            cancellationToken);
        if (configuration is null)
        {
            configuration = new SystemConfigurationEntity { Key = key };
            dbContext.SystemConfigurations.Add(configuration);
        }

        configuration.Value = value;
        configuration.Description = description;
        configuration.Status = 1;
        configuration.IsDelete = 0;
        return configuration;
    }

    private static async Task BackfillEntityAssociationsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userRoles = await dbContext.UserRoles.Where(entity => entity.IsDelete == 0).ToListAsync(cancellationToken);
        foreach (var userRole in userRoles)
        {
            await EnsureAssociationAsync(
                dbContext,
                userRole.UserId,
                userRole.RoleId,
                SecurityAssociationTypes.UserRole,
                cancellationToken);
        }

        var roleMenus = await dbContext.RoleMenus.Where(entity => entity.IsDelete == 0).ToListAsync(cancellationToken);
        foreach (var roleMenu in roleMenus)
        {
            await EnsureAssociationAsync(
                dbContext,
                roleMenu.RoleId,
                roleMenu.MenuId,
                SecurityAssociationTypes.RoleMenu,
                cancellationToken);
        }

        var rolePermissions = await dbContext.RolePermissions.Where(entity => entity.IsDelete == 0).ToListAsync(cancellationToken);
        foreach (var rolePermission in rolePermissions)
        {
            await EnsureAssociationAsync(
                dbContext,
                rolePermission.RoleId,
                rolePermission.PermissionId,
                SecurityAssociationTypes.RolePermission,
                cancellationToken);
        }
    }

    private static async Task EnsureAssociationAsync(
        DefaultDbContext dbContext,
        string sourceEntityId,
        string targetEntityId,
        string associationType,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.EntityAssociations.FirstOrDefaultAsync(
            entity => entity.SourceEntityId == sourceEntityId &&
                entity.TargetEntityId == targetEntityId &&
                entity.AssociationType == associationType,
            cancellationToken);
        if (existing is not null)
        {
            existing.IsDelete = 0;
            existing.UpdateTime = DateTime.UtcNow;
            return;
        }

        dbContext.EntityAssociations.Add(new EntityAssociationEntity
        {
            SourceEntityId = sourceEntityId,
            TargetEntityId = targetEntityId,
            AssociationType = associationType
        });
    }

    private static async Task<MenuEntity> EnsureMenuAsync(
        DefaultDbContext dbContext,
        string roleId,
        string parentId,
        string path,
        string name,
        string component,
        string? icon,
        int sort,
        int type,
        CancellationToken cancellationToken)
    {
        var menu = await dbContext.Menus.FirstOrDefaultAsync(
            entity => entity.Path == path,
            cancellationToken);
        if (menu is null)
        {
            menu = new MenuEntity { Path = path };
            dbContext.Menus.Add(menu);
        }

        menu.ParentId = parentId;
        menu.Name = name;
        menu.Component = component;
        menu.Icon = icon;
        menu.Sort = sort;
        menu.Type = type;
        menu.Status = 1;

        await EnsureRoleMenuAsync(dbContext, roleId, menu.Id, cancellationToken);
        return menu;
    }

    private static async Task DisableMenuAsync(
        DefaultDbContext dbContext,
        string path,
        CancellationToken cancellationToken)
    {
        var menu = await dbContext.Menus.FirstOrDefaultAsync(
            entity => entity.Path == path,
            cancellationToken);
        if (menu is null)
        {
            return;
        }

        menu.Status = 0;
        menu.Type = 2;
    }

    private static async Task EnsureRoleMenuAsync(
        DefaultDbContext dbContext,
        string roleId,
        string menuId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.RoleMenus.AnyAsync(
            entity => entity.RoleId == roleId && entity.MenuId == menuId,
            cancellationToken))
        {
            return;
        }

        dbContext.RoleMenus.Add(new RoleMenuEntity { RoleId = roleId, MenuId = menuId });
    }

    private bool AutoInitialize()
    {
        var environmentValue = Environment.GetEnvironmentVariable("Database__AutoInitialize");
        if (bool.TryParse(environmentValue, out var autoInitialize))
        {
            return autoInitialize;
        }

        return _configuration.GetValue("Database:AutoInitialize", false);
    }
}
