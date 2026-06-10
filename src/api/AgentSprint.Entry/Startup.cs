using System.Text;

using AgentSprint.Domain.Impls.Agile;
using AgentSprint.Domain.Impls.Security;
using AgentSprint.Domain.Impls.Tests;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Tests.Domains;
using AgentSprint.Repository;
using AgentSprint.Repository.DbContexts;
using AgentSprint.Service.Security;

using Air.Cloud.Core.App;
using Air.Cloud.Core.App.Startups;
using Air.Cloud.Core.Attributes;
using Air.Cloud.EntityFrameWork.Core.Configure;
using Air.Cloud.EntityFrameWork.Core.Extensions;
using Air.Cloud.EntityFrameWork.Core.Extensions.DatabaseProvider;
using Air.Cloud.EntityFrameWork.Core.Filters;
using Air.Cloud.EntityFrameWork.MySQL.Configure;
using Air.Cloud.WebApp.Extensions;
using Air.Cloud.WebApp.UnifyResult.Extensions;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AgentSprint.Entry;

[AppStartup(Order = 0)]
public sealed class Startup : AppStartup
{
    /// <summary>
    /// <para>zh-cn:配置 AgentSprint 入口层服务。入口层只保留跨域、认证、数据库访问、统一返回和启动初始化等宿主职责；领域对象与业务 Service 由 Air.Cloud 根据 IEntityDomain、IDynamicService 和生命周期接口自动扫描注册。</para>
    /// <para>en-us:Configures AgentSprint entry-layer services. The entry layer keeps only host concerns such as CORS, authentication, database access, unified results, and startup initialization; domain objects and business services are auto-registered by Air.Cloud through IEntityDomain, IDynamicService, and lifetime interfaces.</para>
    /// </summary>
    /// <param name="services">
    /// <para>zh-cn:应用服务集合。</para>
    /// <para>en-us:Application service collection.</para>
    /// </param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AgentSprintAdmin", policy =>
            {
                policy
                    .WithOrigins("http://localhost:5999", "http://127.0.0.1:5999")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
            options.Filters.Add<AutoSaveChangesFilter>());
        services.AddWebAppUnifyResult<AgentSprintUnifyResultProvider>();
        services.AddOpenApi();

        services.Configure<JwtOptions>(AppCore.Configuration.GetSection("Jwt"));
        var jwtOptions = AppCore.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        services.AddAuthorization();

        services.AddSingleton<IDatabaseConfigure, MySQLDatabaseConfigure>();
        var connectionString = ResolveConnectionString();
        services.AddDatabaseAccessor(options =>
        {
            options.AddDbPool<DefaultDbContext>(
                connectionMetadata: connectionString,
                interceptors:
                [
                    new DbContextSaveChangesInterceptor(),
                    new SqlCommandAuditInterceptor()
                ]);
        }, "AgentSprint.Entry");

        services.AddTransient<IUserDomain, UserDomain>();
        services.AddTransient<IRoleDomain, RoleDomain>();
        services.AddTransient<IMenuDomain, MenuDomain>();
        services.AddTransient<IPermissionDomain, PermissionDomain>();
        services.AddTransient<IAgentTokenDomain, AgentTokenDomain>();
        services.AddTransient<ISystemConfigurationDomain, SystemConfigurationDomain>();
        services.AddTransient<IUserGroupDomain, UserGroupDomain>();
        services.AddTransient<IRoleGroupDomain, RoleGroupDomain>();
        services.AddTransient<IDepartmentDomain, DepartmentDomain>();
        services.AddTransient<IAssignmentDomain, AssignmentDomain>();
        services.AddTransient<IDictionaryTypeDomain, DictionaryTypeDomain>();
        services.AddTransient<IDictionaryItemDomain, DictionaryItemDomain>();
        services.AddTransient<IRuntimeEnvironmentDomain, RuntimeEnvironmentDomain>();
        services.AddTransient<IRuntimeEnvironmentContainerDomain, RuntimeEnvironmentContainerDomain>();
        services.AddTransient<IPromptTemplateDomain, PromptTemplateDomain>();
        services.AddTransient<IEntityAssociationDomain, EntityAssociationDomain>();
        services.AddTransient<IUserRoleDomain, UserRoleDomain>();
        services.AddTransient<IRoleMenuDomain, RoleMenuDomain>();
        services.AddTransient<IRolePermissionDomain, RolePermissionDomain>();

        services.AddTransient<ISprintProjectDomain, SprintProjectDomain>();
        services.AddTransient<ISprintProjectMemberDomain, SprintProjectMemberDomain>();
        services.AddTransient<ISprintProjectEndpointDomain, SprintProjectEndpointDomain>();
        services.AddTransient<ISprintFeatureModuleDomain, SprintFeatureModuleDomain>();
        services.AddTransient<ISprintRequirementDomain, SprintRequirementDomain>();
        services.AddTransient<ISprintSkillDomain, SprintSkillDomain>();
        services.AddTransient<ISprintFeatureSuggestionDomain, SprintFeatureSuggestionDomain>();
        services.AddTransient<ISprintRequirementFeedbackDomain, SprintRequirementFeedbackDomain>();
        services.AddTransient<ISprintRequirementReviewDomain, SprintRequirementReviewDomain>();
        services.AddTransient<ISprintDevelopmentTaskDomain, SprintDevelopmentTaskDomain>();
        services.AddTransient<ISprintBugDomain, SprintBugDomain>();
        services.AddTransient<ISprintTaskLeaseDomain, SprintTaskLeaseDomain>();

        services.AddTransient<ITestPlanDomain, TestPlanDomain>();
        services.AddTransient<ITestExecutionDomain, TestExecutionDomain>();

        services.AddHostedService<DatabaseInitializer>();
    }

    /// <summary>
    /// <para>zh-cn:配置 AgentSprint 请求管道。由于认证与授权必须位于 UseRouting 和 UseEndpoints 之间，入口层显式编排路由、跨域、统一状态码、认证、授权和控制器端点；动态 API 控制器仍由 Air.Cloud MVC 扫描提供。</para>
    /// <para>en-us:Configures the AgentSprint request pipeline. Because authentication and authorization must run between UseRouting and UseEndpoints, the entry layer explicitly orders routing, CORS, unified status codes, authentication, authorization, and controller endpoints; dynamic API controllers are still supplied by Air.Cloud MVC scanning.</para>
    /// </summary>
    /// <param name="app">
    /// <para>zh-cn:应用管道构建器。</para>
    /// <para>en-us:Application pipeline builder.</para>
    /// </param>
    /// <param name="env">
    /// <para>zh-cn:当前宿主环境。</para>
    /// <para>en-us:Current hosting environment.</para>
    /// </param>
    public override void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseCors("AgentSprintAdmin");
        app.UseUnifyResultStatusCodes();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }

    private static string ResolveConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AgentSprintConnectionString");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        connectionString = Environment.GetEnvironmentVariable("AGENTSPRINT_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        connectionString = AppCore.Configuration.GetConnectionString("AgentSprintConnectionString");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        throw new InvalidOperationException("Connection string 'AgentSprintConnectionString' is not configured.");
    }
}
