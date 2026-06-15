using System.Data.Common;

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
    /// zh-cn: 应用启动时执行数据库初始化。禁用 AutoInitialize 时不产生副作用；启用后会先只读检查 admin 用户，已存在则只执行菜单和授权关联演进；只有 admin 不存在，或数据库/用户表尚不存在导致检查失败时，才执行首次初始化。
    /// en-us: Runs database initialization during application startup. When AutoInitialize is disabled it has no side effects; when enabled it first performs a read-only admin-user check and only applies menu and authorization-association evolution when admin exists; first-time initialization only runs when admin is absent or the database/user table does not yet exist.
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
        if (await AdminUserExistsAsync(dbContext, cancellationToken))
        {
            await ApplyExistingDatabaseEvolutionAsync(dbContext, cancellationToken);
            return;
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureAgentTokenTablesAsync(dbContext, cancellationToken);
        await EnsureSystemConfigurationTablesAsync(dbContext, cancellationToken);
        await EnsureSecurityEvolutionTablesAsync(dbContext, cancellationToken);
        await EnsureAgileMvpTablesAsync(dbContext, cancellationToken);
        await EnsureDigitalWorkerTablesAsync(dbContext, cancellationToken);
        await EnsureTestTablesAsync(dbContext, cancellationToken);
        await SeedAdminAsync(dbContext, cancellationToken);
        await SeedSecurityDataAsync(dbContext, cancellationToken);
    }

    private static async Task ApplyExistingDatabaseEvolutionAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            await EnsureAgentTokenTablesAsync(dbContext, cancellationToken);
            await EnsureSystemConfigurationTablesAsync(dbContext, cancellationToken);
            await EnsureSecurityEvolutionTablesAsync(dbContext, cancellationToken);
            await EnsureAgileMvpTablesAsync(dbContext, cancellationToken);
            await EnsureDigitalWorkerTablesAsync(dbContext, cancellationToken);
            await EnsureTestTablesAsync(dbContext, cancellationToken);
        }

        await SeedDashboardMenuAsync(dbContext, cancellationToken);
        await SeedMvpMenuAsync(dbContext, cancellationToken);
        await SeedSystemMenuAsync(dbContext, cancellationToken);

        await SeedSystemConfigurationsAsync(dbContext, cancellationToken);
        await SeedRuntimeManagementSamplesAsync(dbContext, cancellationToken);
        await BackfillEntityAssociationsAsync(dbContext, cancellationToken);
        await EnsureDigitalWorkerTablesAsync(dbContext, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSecurityDataAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await SeedDashboardMenuAsync(dbContext, cancellationToken);
        await SeedMvpMenuAsync(dbContext, cancellationToken);
        await SeedSystemMenuAsync(dbContext, cancellationToken);

        await SeedSystemConfigurationsAsync(dbContext, cancellationToken);
        await SeedRuntimeManagementSamplesAsync(dbContext, cancellationToken);
        await BackfillEntityAssociationsAsync(dbContext, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<bool> MenusExistAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Menus
                .AsNoTracking()
                .AnyAsync(cancellationToken);
        }
        catch (DbException)
        {
            return false;
        }
    }

    private static async Task<bool> AdminUserExistsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Users
                .AsNoTracking()
                .AnyAsync(entity => entity.Username == "admin", cancellationToken);
        }
        catch (DbException)
        {
            return false;
        }
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
              ServerIps varchar(1024) CHARACTER SET utf8mb4 NULL,
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
              ContainerType int NOT NULL,
              ServerIp varchar(64) CHARACTER SET utf8mb4 NULL,
              HostPort int NOT NULL,
              ContainerPort int NOT NULL,
              Protocol varchar(16) CHARACTER SET utf8mb4 NOT NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Prompt text CHARACTER SET utf8mb4 NULL,
              DeployScript text CHARACTER SET utf8mb4 NULL,
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
        await EnsureRuntimeEnvironmentColumnsAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureAgileMvpTablesAsync(DefaultDbContext dbContext, CancellationToken cancellationToken)
    {
        const string projectSql = """
            CREATE TABLE IF NOT EXISTS sprint_project (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
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
              Type varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'development',
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
              AssigneeType int NOT NULL DEFAULT 0,
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
        await EnsureSkillColumnsAsync(dbContext, cancellationToken);
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
        await EnsureGitManagementTablesAsync(dbContext, cancellationToken);
        await BackfillProjectMembersAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureGitManagementTablesAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const string gitAccountSql = """
            CREATE TABLE IF NOT EXISTS git_account (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              Username varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              AccessToken varchar(512) CHARACTER SET utf8mb4 NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_git_account_Code (Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string gitRepositorySql = """
            CREATE TABLE IF NOT EXISTS git_repository (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              RepositoryUrl varchar(512) CHARACTER SET utf8mb4 NOT NULL,
              DefaultBranch varchar(64) CHARACTER SET utf8mb4 NULL,
              GitAccountId varchar(64) CHARACTER SET utf8mb4 NULL,
              LocalPath varchar(512) CHARACTER SET utf8mb4 NULL,
              Description varchar(512) CHARACTER SET utf8mb4 NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_git_repository_Code (Code)
            ) CHARACTER SET=utf8mb4;
            """;

        const string gitBranchOperationSql = """
            CREATE TABLE IF NOT EXISTS git_branch_operation (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              RepositoryId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              AccountId varchar(64) CHARACTER SET utf8mb4 NULL,
              OperationType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              BranchName varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              SourceBranch varchar(128) CHARACTER SET utf8mb4 NULL,
              BackupBranch varchar(128) CHARACTER SET utf8mb4 NULL,
              CommitHash varchar(64) CHARACTER SET utf8mb4 NULL,
              CommitMessage varchar(512) CHARACTER SET utf8mb4 NULL,
              PushedAt datetime(6) NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              Message varchar(2048) CHARACTER SET utf8mb4 NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_git_branch_operation_RepositoryId_OperationType (RepositoryId, OperationType),
              INDEX IX_git_branch_operation_RepositoryId_BranchName (RepositoryId, BranchName)
            ) CHARACTER SET=utf8mb4;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(gitAccountSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(gitRepositorySql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(gitBranchOperationSql, cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "GitRepositoryId",
            "ALTER TABLE sprint_project ADD COLUMN GitRepositoryId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sprint_project",
            "GitAccountId",
            "ALTER TABLE sprint_project ADD COLUMN GitAccountId varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
    }

    private static async Task EnsureDigitalWorkerTablesAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        const string digitalWorkerSql = """
            CREATE TABLE IF NOT EXISTS digital_worker (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              AgentUserId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              AgentTokenId varchar(64) CHARACTER SET utf8mb4 NULL,
              ProjectIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              EndpointIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL,
              EmployeeType varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'development',
              WorkerType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              MaxConcurrentRuns int NOT NULL,
              HeartbeatTimeoutSeconds int NOT NULL,
              PollIntervalSeconds int NOT NULL DEFAULT 15,
              IdleMaxIntervalSeconds int NOT NULL DEFAULT 180,
              MaxRunMinutes int NOT NULL DEFAULT 60,
              WorkspaceRoot varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT '/workspaces',
              RunsRoot varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT '/runs',
              CodexHome varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT '/codex-home',
              SandboxMode varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'workspace-write',
              RunSmokeOnStartup tinyint(1) NOT NULL DEFAULT 0,
              SmokePrompt varchar(1024) CHARACTER SET utf8mb4 NULL,
              CodexProvider varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'openai',
              CodexModel varchar(128) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'gpt-5.4',
              OpenAiBaseUrl varchar(512) CHARACTER SET utf8mb4 NULL,
              ConfigVersion int NOT NULL DEFAULT 1,
              Description varchar(1024) CHARACTER SET utf8mb4 NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              UNIQUE INDEX IX_digital_worker_Code (Code),
              INDEX IX_digital_worker_AgentUserId_Status (AgentUserId, Status)
            ) CHARACTER SET=utf8mb4;
            """;

        const string workerSessionSql = """
            CREATE TABLE IF NOT EXISTS worker_session (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              WorkerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              InstanceId varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              HostName varchar(128) CHARACTER SET utf8mb4 NULL,
              ContainerId varchar(128) CHARACTER SET utf8mb4 NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CodexVersion varchar(128) CHARACTER SET utf8mb4 NULL,
              GitVersion varchar(128) CHARACTER SET utf8mb4 NULL,
              DotnetVersion varchar(128) CHARACTER SET utf8mb4 NULL,
              NodeVersion varchar(128) CHARACTER SET utf8mb4 NULL,
              ConfigTomlExists tinyint(1) NOT NULL,
              CodexHome varchar(512) CHARACTER SET utf8mb4 NULL,
              WorkspaceRoot varchar(512) CHARACTER SET utf8mb4 NULL,
              RunsRoot varchar(512) CHARACTER SET utf8mb4 NULL,
              CurrentRunId varchar(64) CHARACTER SET utf8mb4 NULL,
              ErrorSummary varchar(1024) CHARACTER SET utf8mb4 NULL,
              LastHeartbeatAt datetime(6) NULL,
              StartedAt datetime(6) NOT NULL,
              StoppedAt datetime(6) NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_worker_session_WorkerId_Status (WorkerId, Status),
              INDEX IX_worker_session_WorkerId_InstanceId (WorkerId, InstanceId)
            ) CHARACTER SET=utf8mb4;
            """;

        const string workerCommandSql = """
            CREATE TABLE IF NOT EXISTS worker_command (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              WorkerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              SessionId varchar(64) CHARACTER SET utf8mb4 NULL,
              CommandType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              PayloadJson text CHARACTER SET utf8mb4 NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              AckedAt datetime(6) NULL,
              StartedAt datetime(6) NULL,
              CompletedAt datetime(6) NULL,
              ExpiresAt datetime(6) NULL,
              ResultJson text CHARACTER SET utf8mb4 NULL,
              Error varchar(1024) CHARACTER SET utf8mb4 NULL,
              CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_worker_command_WorkerId_Status (WorkerId, Status),
              INDEX IX_worker_command_SessionId_Status (SessionId, Status)
            ) CHARACTER SET=utf8mb4;
            """;

        const string workerRunSql = """
            CREATE TABLE IF NOT EXISTS worker_run (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              WorkerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              SessionId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              CommandId varchar(64) CHARACTER SET utf8mb4 NULL,
              RunType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              TargetType varchar(32) CHARACTER SET utf8mb4 NULL,
              TargetId varchar(64) CHARACTER SET utf8mb4 NULL,
              Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              WorkspacePath varchar(512) CHARACTER SET utf8mb4 NULL,
              PromptPath varchar(512) CHARACTER SET utf8mb4 NULL,
              StdoutPath varchar(512) CHARACTER SET utf8mb4 NULL,
              StderrPath varchar(512) CHARACTER SET utf8mb4 NULL,
              FinalPath varchar(512) CHARACTER SET utf8mb4 NULL,
              ManifestPath varchar(512) CHARACTER SET utf8mb4 NULL,
              ExitCode int NULL,
              TimedOut tinyint(1) NOT NULL,
              Error varchar(1024) CHARACTER SET utf8mb4 NULL,
              StartedAt datetime(6) NOT NULL,
              CompletedAt datetime(6) NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_worker_run_WorkerId_SessionId (WorkerId, SessionId),
              INDEX IX_worker_run_TargetType_TargetId (TargetType, TargetId)
            ) CHARACTER SET=utf8mb4;
            """;

        const string workerEventSql = """
            CREATE TABLE IF NOT EXISTS worker_event (
              Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              WorkerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              SessionId varchar(64) CHARACTER SET utf8mb4 NULL,
              RunId varchar(64) CHARACTER SET utf8mb4 NULL,
              EventType varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              Level varchar(16) CHARACTER SET utf8mb4 NOT NULL,
              Message varchar(1024) CHARACTER SET utf8mb4 NOT NULL,
              PayloadJson text CHARACTER SET utf8mb4 NULL,
              CreateTime datetime(6) NOT NULL,
              UpdateTime datetime(6) NULL,
              IsDelete int NOT NULL,
              PRIMARY KEY (Id),
              INDEX IX_worker_event_WorkerId_CreateTime (WorkerId, CreateTime)
            ) CHARACTER SET=utf8mb4;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(digitalWorkerSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(workerSessionSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(workerCommandSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(workerRunSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(workerEventSql, cancellationToken);
        await EnsureDigitalWorkerColumnsAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureDigitalWorkerColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "EmployeeType",
            "ALTER TABLE digital_worker ADD COLUMN EmployeeType varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'development' AFTER SkillIds;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "PollIntervalSeconds",
            "ALTER TABLE digital_worker ADD COLUMN PollIntervalSeconds int NOT NULL DEFAULT 15 AFTER HeartbeatTimeoutSeconds;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "IdleMaxIntervalSeconds",
            "ALTER TABLE digital_worker ADD COLUMN IdleMaxIntervalSeconds int NOT NULL DEFAULT 180 AFTER PollIntervalSeconds;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "MaxRunMinutes",
            "ALTER TABLE digital_worker ADD COLUMN MaxRunMinutes int NOT NULL DEFAULT 60 AFTER IdleMaxIntervalSeconds;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "WorkspaceRoot",
            "ALTER TABLE digital_worker ADD COLUMN WorkspaceRoot varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT '/workspaces' AFTER MaxRunMinutes;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "RunsRoot",
            "ALTER TABLE digital_worker ADD COLUMN RunsRoot varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT '/runs' AFTER WorkspaceRoot;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "CodexHome",
            "ALTER TABLE digital_worker ADD COLUMN CodexHome varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT '/codex-home' AFTER RunsRoot;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "SandboxMode",
            "ALTER TABLE digital_worker ADD COLUMN SandboxMode varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'workspace-write' AFTER CodexHome;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "RunSmokeOnStartup",
            "ALTER TABLE digital_worker ADD COLUMN RunSmokeOnStartup tinyint(1) NOT NULL DEFAULT 0 AFTER SandboxMode;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "SmokePrompt",
            "ALTER TABLE digital_worker ADD COLUMN SmokePrompt varchar(1024) CHARACTER SET utf8mb4 NULL AFTER RunSmokeOnStartup;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "CodexProvider",
            "ALTER TABLE digital_worker ADD COLUMN CodexProvider varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'openai' AFTER SmokePrompt;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "CodexModel",
            "ALTER TABLE digital_worker ADD COLUMN CodexModel varchar(128) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'gpt-5.4' AFTER CodexProvider;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "OpenAiBaseUrl",
            "ALTER TABLE digital_worker ADD COLUMN OpenAiBaseUrl varchar(512) CHARACTER SET utf8mb4 NULL AFTER CodexModel;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "digital_worker",
            "ConfigVersion",
            "ALTER TABLE digital_worker ADD COLUMN ConfigVersion int NOT NULL DEFAULT 1 AFTER OpenAiBaseUrl;",
            cancellationToken);
        await EnsurePromptTemplateAsync(
            dbContext,
            "digital_worker_task_execution",
            "数字员工任务执行提示词",
            BuildDigitalWorkerTaskExecutionPromptTemplate(),
            "AgentSprint.Worker 通过 API 获取任务上下文后写入 Codex 的数字员工提示词。",
            30,
            cancellationToken);
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

    private static async Task EnsureSkillColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sprint_skill",
            "Type",
            "ALTER TABLE sprint_skill ADD COLUMN Type varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'development';",
            cancellationToken);
    }

    private static async Task EnsureRuntimeEnvironmentColumnsAsync(
        DefaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "sys_runtime_environment",
            "ServerIps",
            "ALTER TABLE sys_runtime_environment ADD COLUMN ServerIps varchar(1024) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sys_runtime_environment_container",
            "ContainerType",
            "ALTER TABLE sys_runtime_environment_container ADD COLUMN ContainerType int NOT NULL DEFAULT 0;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sys_runtime_environment_container",
            "ServerIp",
            "ALTER TABLE sys_runtime_environment_container ADD COLUMN ServerIp varchar(64) CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sys_runtime_environment_container",
            "Prompt",
            "ALTER TABLE sys_runtime_environment_container ADD COLUMN Prompt text CHARACTER SET utf8mb4 NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "sys_runtime_environment_container",
            "DeployScript",
            "ALTER TABLE sys_runtime_environment_container ADD COLUMN DeployScript text CHARACTER SET utf8mb4 NULL;",
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
        await EnsureColumnAsync(
            dbContext,
            "sprint_development_task",
            "AssigneeType",
            "ALTER TABLE sprint_development_task ADD COLUMN AssigneeType int NOT NULL DEFAULT 0;",
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

        PreserveMenuName(parentMenu, "Sprint");
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
            "/sprint/mvp",
            "/sprint/skills"
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

        var gitGroup = await EnsureMenuAsync(
            dbContext,
            role.Id,
            string.Empty,
            "/sprint/git",
            "GitManagementGroup",
            string.Empty,
            "lucide:git-branch",
            50,
            0,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, gitGroup.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

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

        var gitAccountsMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            gitGroup.Id,
            "/sprint/git/accounts",
            "SprintGitAccounts",
            "/sprint/git/accounts/index",
            "lucide:key-round",
            10,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, gitAccountsMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);

        var gitRepositoriesMenu = await EnsureMenuAsync(
            dbContext,
            role.Id,
            gitGroup.Id,
            "/sprint/git/repositories",
            "SprintGitRepositories",
            "/sprint/git/repositories/index",
            "lucide:git-fork",
            20,
            1,
            cancellationToken);
        await EnsureAssociationAsync(dbContext, role.Id, gitRepositoriesMenu.Id, SecurityAssociationTypes.RoleMenu, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "Sprint:GitAccount:Manage", "Git account management", gitAccountsMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "Sprint:GitRepository:Manage", "Git repository management", gitRepositoriesMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "Sprint:GitRepository:BranchCreate", "Create Git repository branch", gitRepositoriesMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "Sprint:GitRepository:BranchDelete", "Backup and delete Git repository branch", gitRepositoriesMenu.Id, cancellationToken);
        await EnsurePermissionAsync(dbContext, role.Id, "Sprint:GitRepository:PushRecord:Read", "Read Git repository push records", gitRepositoriesMenu.Id, cancellationToken);

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

        environment.Name = "娴嬭瘯鐜";
        environment.EnvironmentType = "test";
        environment.Description = "AgentSprint 默认测试环境，按前端、API、MCP 与部署路径拆分维护。";
        environment.FrontendUrl = "http://192.168.80.101:5999";
        environment.ApiBaseUrl = "http://192.168.80.101:5000";
        environment.FrontendProxyApiUrl = "http://192.168.80.101:5999/api";
        environment.McpEndpoint = "http://192.168.80.101:5010/mcp";
        environment.ServerIps = "192.168.80.101";
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

        await EnsureRuntimeContainerAsync(dbContext, environment.Id, "agentsprint-admin", "192.168.80.101", 5999, 80, 10, cancellationToken);
        await EnsureRuntimeContainerAsync(dbContext, environment.Id, "agentsprint-api", "192.168.80.101", 5000, 5000, 20, cancellationToken);
        await EnsureRuntimeContainerAsync(dbContext, environment.Id, "agentsprint-mcp", "192.168.80.101", 5010, 5010, 30, cancellationToken);

        await EnsurePromptTemplateAsync(
            dbContext,
            "mcp_setup",
            "MCP 接入提示词",
            BuildCodexMcpSetupPromptTemplate(),
            "Codex agentsprint MCP 接入配置提示词。",
            10,
            cancellationToken);
        await EnsurePromptTemplateAsync(
            dbContext,
            "task_execution",
            "任务推进提示词",
            BuildCodexTaskExecutionPromptTemplate(),
            "Codex 任务推进提示词。",
            20,
            cancellationToken);
        await EnsurePromptTemplateAsync(
            dbContext,
            "digital_worker_task_execution",
            "数字员工任务执行提示词",
            BuildDigitalWorkerTaskExecutionPromptTemplate(),
            "AgentSprint.Worker 通过 API 获取任务上下文后写入 Codex 的数字员工提示词。",
            30,
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
        var prompt = dbContext.PromptTemplates.Local.FirstOrDefault(
                entity => entity.AgentEnvironment == "codex" && entity.Code == code)
            ?? await dbContext.PromptTemplates.FirstOrDefaultAsync(
                entity => entity.AgentEnvironment == "codex" && entity.Code == code,
                cancellationToken);
        if (prompt is null)
        {
            prompt = new PromptTemplateEntity
            {
                AgentEnvironment = "codex",
                Code = code,
                Name = name,
                Content = content,
                Description = description,
                Sort = sort,
                Status = 1,
                IsDelete = 0
            };
            dbContext.PromptTemplates.Add(prompt);
            return;
        }

        if (prompt.IsDelete == 1)
        {
            prompt.IsDelete = 0;
        }
    }

    private static string BuildCodexMcpSetupPromptTemplate()
    {
        return """
               你现在位于 AgentSprint 项目工作区，请只完成 Codex 的 agentsprint MCP 接入配置，不修改项目代码。

               目标：
               将 agentsprint 远程 HTTP MCP 配置到 Codex，使后续任务可以通过 MCP 自动拉取任务上下文并推进。

               请按下面流程执行：
               1. 检查当前项目工作区是否为 AgentSprint 项目。
               2. 检查 `~/.codex/config.toml` 中是否已有 `[mcp_servers.agentsprint]`。
               3. 如果不存在则新增；如果已存在则更新为下面配置。
               4. 保留现有其他 MCP 配置，不要覆盖 `node_repl` 等已有配置。
               5. 默认只配置 MCP endpoint 和 Authorization，不要默认写入 `X-AgentSprint-Api-Base-Url`。
               6. 只有在用户明确提供“远程 MCP 服务可访问的 AgentSprint API 地址”时，才写入 `X-AgentSprint-Api-Base-Url`。
               7. 不要把 `http://localhost:5000` 固定写入 `X-AgentSprint-Api-Base-Url`。
               8. Codex HTTP MCP 请求头必须使用 `http_headers` 字段，不要使用 `[mcp_servers.agentsprint.headers]` 子表。
               9. 配置完成后，验证 Codex 是否能识别 agentsprint MCP；如果需要新对话或重启 Codex 才能生效，请明确说明。

               需要写入的 Codex TOML 配置为：

               ```toml
               [mcp_servers.agentsprint]
               url = "{{mcpEndpoint}}"
               http_headers = { Authorization = "{{agentToken}}" }
               ```

               可选覆盖配置：
               仅当用户明确提供远程 MCP 服务可访问的 AgentSprint API 地址时，才追加到 `http_headers`：

               ```toml
               http_headers = {
                 Authorization = "{{agentToken}}",
                 "X-AgentSprint-Api-Base-Url" = "<远程 MCP 服务可访问的 AgentSprint API 地址>"
               }
               ```

               如果当前 Codex 版本不支持 HTTP MCP 的 `http_headers` 字段，请不要继续猜测配置方式，直接说明阻塞点。
               """;
    }

    private static string BuildCodexTaskExecutionPromptTemplate()
    {
        return """
               你正在推进 AgentSprint 平台任务，请通过 agentsprint MCP 按任务 ID 加载上下文，不要依赖这段静态文本获取需求详情。

               任务标识：
               项目编码：{{projectCode}}
               任务 ID：{{taskId}}
               仓库引用：{{repositoryReference}}

               执行顺序：
               1. 调用 agentsprint.register_session，参数包含 project_code = "{{projectCode}}"。
               2. 调用 agentsprint.get_mcp_tool_guide，参数包含 format = "full"，读取工具用途、参数、返回结果和推荐流程。
               3. 调用 agentsprint.get_agent_skill_pack，参数包含 project_code = "{{projectCode}}"，获取 Skill、后端规则、前端规则和验证命令。
               4. 调用 agentsprint.get_task_prompt，参数包含 task_id = "{{taskId}}"，加载 task_detail、requirement_detail 和任务提示上下文。
               5. 按 MCP 返回的任务、需求、Skill 包和验证命令完成实现，不从这段静态提示词解析需求正文。
               6. 完成后运行相关后端测试和前端类型检查，并调用 agentsprint.complete_my_task，参数包含 task_id = "{{taskId}}" 回写任务状态。
               7. 读取 complete_my_task 返回的 next_work 作为平台状态参考，但不要调用 agentsprint.claim_next_work，也不要继续处理新的 bug 或 task。
               8. 回写完成后停止接取新的任务；如已注册 session，请调用 agentsprint.close_session 结束当前会话。
               9. 最终回复只报告当前任务的修改点、验证命令和完成状态，不主动轮询 agentsprint.get_next_work。
               10. 只有用户再次明确要求继续推进或接取新任务时，才重新查询或领取下一项工作。

               约束：不要在提示词、代码、日志或 MCP 响应中写入 SSH 私钥、数据库密码、Agent Token 等敏感明文。
               """;
    }

    private static string BuildDigitalWorkerTaskExecutionPromptTemplate()
    {
        return """
               You are AgentSprint digital worker {{workerName}} ({{workerCode}}), running under AgentSprint.Worker through the platform API.

               Boundaries:
               1. Do not connect to or call AgentSprint MCP tools.
               2. Do not claim new tasks or bugs.
               3. Do not write Agent Token, database passwords, SSH private keys, or other secrets into code, logs, or the final response.
               4. Stop after the current target is handled. Worker will call {{completionApiPath}} to update platform state.

               Target:
               - Type: {{targetType}}
               - Target ID: {{targetId}}
               - Project: {{projectName}} ({{projectCode}})
               - Repository: {{repositoryReference}}
               - Workspace: {{workspacePath}}

               Requirement:
               - Requirement ID: {{requirementId}}
               - Title: {{requirementTitle}}
               - Status: {{requirementStatus}}
               - Endpoint: {{endpointId}}
               - Module: {{moduleId}}

               Requirement description:
               {{requirementDescription}}

               Development task:
               - Task ID: {{taskId}}
               - Title: {{taskTitle}}

               Task description:
               {{taskDescription}}

               Bug fix:
               - Bug ID: {{bugId}}
               - Title: {{bugTitle}}
               - Environment: {{bugEnvironment}}
               - Severity: {{bugSeverity}}

               Bug description:
               {{bugDescription}}

               Skills and project rules:
               {{skillContext}}

               Execution requirements:
               1. Inspect the local workspace and project files first.
               2. Make the smallest necessary change for the task or bug above.
               3. Run relevant verification commands. If verification cannot run, explain the blocker.
               4. In the final response, report only changed points, verification commands, and completion status.
               5. {{completionInstruction}}
               """;
    }

    private static async Task EnsureRuntimeContainerAsync(
        DefaultDbContext dbContext,
        string runtimeEnvironmentId,
        string name,
        string serverIp,
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

        container.ContainerType = 0;
        container.ServerIp = serverIp;
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
        await EnsureConfigurationAsync(
            dbContext,
            "AiPlatform:openai",
            """{"name":"OpenAI","provider":"openai","model":"gpt-5.4","openAiBaseUrl":"https://api.openai.com/v1","sort":10}""",
            "Default AI platform used by digital workers.",
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
        await EnsureDictionaryTypeAsync(
            dbContext,
            "runtime_container_type",
            "运行服务类型",
            "Runtime service host/container type options used by environment service management.",
            30,
            cancellationToken,
            ("0", "Docker", 10),
            ("1", "K3S", 20),
            ("2", "K8S", 30),
            ("3", "Tomcat", 40),
            ("4", "Nginx", 50),
            ("9", "Other", 90));
        await EnsureDictionaryTypeAsync(
            dbContext,
            "ai_platform_support",
            "AI平台支持",
            "AI platform options used by prompt template management.",
            40,
            cancellationToken,
            ("codex", "Codex", 10),
            ("claude_code", "ClaudeCode", 20),
            ("work_buddy", "WorkBuddy", 30),
            ("open_claw", "OpenClaw", 40));
        await EnsureDictionaryTypeAsync(
            dbContext,
            "digital_worker_employee_type",
            "数字员工类型",
            "Digital worker employee type options used by worker management.",
            50,
            cancellationToken,
            ("operations", "运维", 10),
            ("development", "研发", 20),
            ("audit", "审计", 30),
            ("test", "测试", 40),
            ("product", "产品", 50));
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

        PreserveMenuName(system, "System");
        system.Icon = "lucide:settings";
        system.Sort = 90;
        system.Type = 0;
        system.Status = 1;
        system.IsDelete = 0;

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
        await EnsureHiddenSystemMenuAsync(dbContext, role.Id, system.Id, "/system/roles/authorize/:id", "SystemRoleAuthorize", "/system/roles/authorize", "lucide:shield-check", 21, cancellationToken);
        var menusMenu = await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/menus", "SystemMenus", "/system/menus/index", "lucide:menu", 30, cancellationToken);
        await DisableMenuAsync(dbContext, "/system/permissions", cancellationToken);
        var dictionariesMenu = await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/dictionaries", "SystemDictionaries", "/system/dictionaries/index", "lucide:book-open-text", 40, cancellationToken);
        var configurationsMenu = await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/configurations", "SystemConfigurations", "/system/configurations/index", "lucide:sliders-horizontal", 45, cancellationToken);
        await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/departments", "SystemDepartments", "/system/departments/index", "lucide:network", 50, cancellationToken);
        await EnsureSystemMenuAsync(dbContext, role.Id, system.Id, "/system/assignments", "SystemAssignments", "/system/assignments/index", "lucide:briefcase-business", 60, cancellationToken);
        await DisableMenuAsync(dbContext, "/system/runtime-environments", cancellationToken);
        await DisableMenuAsync(dbContext, "/system/prompt-templates", cancellationToken);

        await MoveMenuAsync(dbContext, "/global-config/environments", "/operations/environments", cancellationToken);

        var operationManagement = await dbContext.Menus.FirstOrDefaultAsync(entity => entity.Path == "/operations", cancellationToken);
        if (operationManagement is null)
        {
            operationManagement = new MenuEntity { Path = "/operations" };
            dbContext.Menus.Add(operationManagement);
        }

        PreserveMenuName(operationManagement, "OperationManagement");
        operationManagement.Icon = "lucide:server-cog";
        operationManagement.Sort = 91;
        operationManagement.Type = 0;
        operationManagement.Status = 1;
        operationManagement.IsDelete = 0;

        await EnsureRoleMenuAsync(dbContext, role.Id, operationManagement.Id, cancellationToken);
        await EnsureAssociationAsync(
            dbContext,
            role.Id,
            operationManagement.Id,
            SecurityAssociationTypes.RoleMenu,
            cancellationToken);

        await EnsureSystemMenuAsync(dbContext, role.Id, operationManagement.Id, "/operations/scripts", "OperationScripts", "/operations/scripts/index", "lucide:file-terminal", 10, cancellationToken);
        var runtimeEnvironmentsMenu = await EnsureSystemMenuAsync(dbContext, role.Id, operationManagement.Id, "/operations/environments", "OperationEnvironments", "/system/runtime-environments/index", "lucide:server-cog", 20, cancellationToken);
        await DisableMenuAsync(dbContext, "/global-config/environments", cancellationToken);

        var globalConfig = await dbContext.Menus.FirstOrDefaultAsync(entity => entity.Path == "/global-config", cancellationToken);
        if (globalConfig is null)
        {
            globalConfig = new MenuEntity { Path = "/global-config" };
            dbContext.Menus.Add(globalConfig);
        }

        PreserveMenuName(globalConfig, "GlobalConfig");
        globalConfig.Icon = "lucide:sliders-horizontal";
        globalConfig.Sort = 92;
        globalConfig.Type = 0;
        globalConfig.Status = 1;
        globalConfig.IsDelete = 0;

        await EnsureRoleMenuAsync(dbContext, role.Id, globalConfig.Id, cancellationToken);
        await EnsureAssociationAsync(
            dbContext,
            role.Id,
            globalConfig.Id,
            SecurityAssociationTypes.RoleMenu,
            cancellationToken);

        var aiPlatformsMenu = await EnsureSystemMenuAsync(dbContext, role.Id, globalConfig.Id, "/global-config/ai-platforms", "GlobalConfigAiPlatforms", "/system/ai-platforms/index", "lucide:cpu", 10, cancellationToken);
        var promptTemplatesMenu = await EnsureSystemMenuAsync(dbContext, role.Id, globalConfig.Id, "/global-config/prompt-templates", "GlobalConfigPromptTemplates", "/system/prompt-templates/index", "lucide:message-square-code", 20, cancellationToken);
        var skillsMenu = await EnsureSystemMenuAsync(dbContext, role.Id, globalConfig.Id, "/global-config/skills", "GlobalConfigSkills", "/sprint/skills/index", "lucide:brain-circuit", 30, cancellationToken);

        var security = await dbContext.Menus.FirstOrDefaultAsync(entity => entity.Path == "/security", cancellationToken);
        if (security is null)
        {
            security = new MenuEntity { Path = "/security" };
            dbContext.Menus.Add(security);
        }

        PreserveMenuName(security, "Security");
        security.Icon = "lucide:shield-check";
        security.Sort = 95;
        security.Type = 0;
        security.Status = 1;
        security.IsDelete = 0;

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
        await EnsurePermissionAsync(dbContext, role.Id, "System:AiPlatform:Manage", "AI platform management", aiPlatformsMenu.Id, cancellationToken);
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
        PreserveMenuName(menu, name);
        menu.Component = component;
        menu.Icon = icon;
        menu.Sort = sort;
        menu.Type = type;
        menu.Status = 1;
        menu.IsDelete = 0;

        await EnsureRoleMenuAsync(dbContext, roleId, menu.Id, cancellationToken);
        return menu;
    }

    private static void PreserveMenuName(MenuEntity menu, string defaultName)
    {
        if (string.IsNullOrWhiteSpace(menu.Name))
        {
            menu.Name = defaultName;
        }
    }

    private static async Task<MenuEntity> EnsureHiddenSystemMenuAsync(
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
        var menu = await EnsureSystemMenuAsync(dbContext, roleId, parentId, path, name, component, icon, sort, cancellationToken);
        menu.Type = 2;
        menu.Status = 1;
        return menu;
    }

    private static async Task MoveMenuAsync(
        DefaultDbContext dbContext,
        string oldPath,
        string newPath,
        CancellationToken cancellationToken)
    {
        var oldMenu = await dbContext.Menus.FirstOrDefaultAsync(
            entity => entity.Path == oldPath,
            cancellationToken);
        if (oldMenu is null)
        {
            return;
        }

        var newMenu = await dbContext.Menus.FirstOrDefaultAsync(
            entity => entity.Path == newPath,
            cancellationToken);
        if (newMenu is null)
        {
            oldMenu.Path = newPath;
            return;
        }

        if (oldMenu.Id != newMenu.Id)
        {
            oldMenu.Status = 0;
            oldMenu.Type = 2;
        }
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
            entity => entity.RoleId == roleId && entity.MenuId == menuId && entity.IsDelete == 0,
            cancellationToken))
        {
            return;
        }

        var deleted = await dbContext.RoleMenus.FirstOrDefaultAsync(
            entity => entity.RoleId == roleId && entity.MenuId == menuId,
            cancellationToken);
        if (deleted is not null)
        {
            deleted.IsDelete = 0;
            deleted.UpdateTime = DateTime.UtcNow;
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
