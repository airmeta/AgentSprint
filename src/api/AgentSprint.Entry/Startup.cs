using System.Text;

using AgentSprint.Domain.Impls.Agile;
using AgentSprint.Entry.Development;
using AgentSprint.Domain.Impls.Security;
using AgentSprint.Domain.Impls.Tests;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Tests.Domains;
using AgentSprint.Repository;
using AgentSprint.Service.Impls.AgileServices;
using AgentSprint.Repository.DbContexts;
using AgentSprint.Service.Impls.AuthServices;
using AgentSprint.Service.Impls.SecurityServices;
using AgentSprint.Service.Impls.TestServices;
using AgentSprint.Service.Impls.UserServices;
using AgentSprint.Service.Security;
using AgentSprint.Service.Services.AgileServices;
using AgentSprint.Service.Services.AuthServices;
using AgentSprint.Service.Services.SecurityServices;
using AgentSprint.Service.Services.TestServices;
using AgentSprint.Service.Services.UserServices;

using Air.Cloud.Core.App;
using Air.Cloud.Core.App.Startups;
using Air.Cloud.EntityFrameWork.Core.Configure;
using Air.Cloud.EntityFrameWork.Core.Extensions;
using Air.Cloud.EntityFrameWork.Core.Extensions.DatabaseProvider;
using Air.Cloud.EntityFrameWork.Core.Filters;
using Air.Cloud.EntityFrameWork.MySQL.Configure;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AgentSprint.Entry;

public sealed class Startup : AppStartup
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
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
        services.AddControllers(options =>
        {
            options.Filters.Add<AutoSaveChangesFilter>();
        });
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

        services.AddTransient<IRoleDomain, RoleDomain>();
        services.AddTransient<IMenuDomain, MenuDomain>();
        services.AddTransient<IPermissionDomain, PermissionDomain>();
        services.AddTransient<IAgentTokenDomain, AgentTokenDomain>();
        services.AddTransient<ISystemConfigurationDomain, SystemConfigurationDomain>();
        services.AddTransient<IUserGroupDomain, UserGroupDomain>();
        services.AddTransient<IRoleGroupDomain, RoleGroupDomain>();
        services.AddTransient<IDepartmentDomain, DepartmentDomain>();
        services.AddTransient<IAssignmentDomain, AssignmentDomain>();
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

        if (UseInMemorySecurity())
        {
            services.AddTransient<IUserDomain, DevelopmentUserDomain>();
            services.AddTransient<IAuthService, DevelopmentAuthService>();
            services.AddTransient<IUserService, DevelopmentUserService>();
            services.AddTransient<ISystemManagementService, DevelopmentSystemManagementService>();
        }
        else
        {
            services.AddTransient<IUserDomain, UserDomain>();
            services.AddTransient<AuthService>();
            services.AddTransient<IAuthService, AuthService>();
            services.AddTransient<IUserService, UserService>();
            services.AddTransient<ISystemManagementService, SystemManagementService>();
        }

        services.AddTransient<IRequirementDecompositionService, RequirementDecompositionService>();
        services.AddTransient<ISecurityAuthorizationService, SecurityAuthorizationService>();
        services.AddTransient<IAgentTokenService, AgentTokenService>();
        services.AddTransient<ISystemConfigurationService, SystemConfigurationService>();
        services.AddTransient<IAgileMvpService, AgileMvpService>();
        services.AddTransient<ITestService, TestService>();
        services.AddHostedService<DatabaseInitializer>();
    }

    public override void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseCors("AgentSprintAdmin");
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

    private static bool UseInMemorySecurity()
    {
        var environmentValue = Environment.GetEnvironmentVariable("Database__UseInMemorySecurity");
        if (bool.TryParse(environmentValue, out var useInMemorySecurity))
        {
            return useInMemorySecurity;
        }

        return AppCore.Configuration.GetValue("Database:UseInMemorySecurity", false);
    }
}
