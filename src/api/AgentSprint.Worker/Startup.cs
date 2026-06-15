using AgentSprint.Worker.Options;
using AgentSprint.Worker.Actors;
using AgentSprint.Worker.Services;

using Air.Cloud.Core.App.Startups;
using Air.Cloud.Core.Attributes;
using Air.Cloud.Modules.Akka.Extensions;
using Air.Cloud.Modules.Akka.Options;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSprint.Worker;

[AppStartup(Order = 0)]
public sealed class Startup : AppStartup
{
    /// <summary>
    /// <para>zh-cn:配置数字员工受控端控制台宿主服务。该入口只注册 Host 后台服务、配置模型、HTTP 客户端、环境探针、Codex 执行器和运行目录记录器，不引入 WebApp 入口，避免 HostApp 与 WebApp 的 Air.Cloud 标准实现冲突。</para>
    /// <para>en-us:Configures the digital-worker controlled console host. This entry registers only Host background services, option models, HTTP clients, environment probing, the Codex runner, and run-directory logging, and does not add the WebApp entry so HostApp and WebApp Air.Cloud standard implementations cannot conflict.</para>
    /// </summary>
    /// <param name="services">
    /// <para>zh-cn:Host 应用服务集合。</para>
    /// <para>en-us:Host application service collection.</para>
    /// </param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<WorkerOptions>()
            .BindConfiguration("Worker")
            .Validate(options => options.PollIntervalSeconds > 0, "Worker:PollIntervalSeconds must be greater than 0.")
            .Validate(options => options.MaxRunMinutes > 0, "Worker:MaxRunMinutes must be greater than 0.")
            .Validate(options => options.CodexIdleTimeoutSeconds > 0, "Worker:CodexIdleTimeoutSeconds must be greater than 0.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CodexExecutable), "Worker:CodexExecutable is required.")
            .ValidateOnStart();

        services.AddOptions<AgentSprintOptions>()
            .BindConfiguration("AgentSprint")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiBaseUrl), "AgentSprint:ApiBaseUrl is required.")
            .ValidateOnStart();

        services.AddOptions<AkkaSettingsOptions>()
            .BindConfiguration("AkkaSettings");
        services.AddHostedService<WorkerActorDependencyInitializer>();
        services.AddAkkaCluster();

        services.AddHttpClient<AgentSprintApiClient>();
        services.AddSingleton<WorkerRuntimeConfigApplier>();
        services.AddSingleton<WorkerRunLogger>();
        services.AddSingleton<WorkerEnvironmentProbe>();
        services.AddSingleton<GitWorkspaceManager>();
        services.AddSingleton<CodexProcessRunner>();
        services.AddHostedService<AgentSprintWorkerService>();
    }

    /// <summary>
    /// <para>zh-cn:满足 Air.Cloud AppStartup 的请求管道契约。数字员工受控端是 HostApp 控制台服务，不暴露 HTTP 管道，因此这里保持空实现，避免引入 WebApp 入口标准。</para>
    /// <para>en-us:Satisfies the Air.Cloud AppStartup request-pipeline contract. The digital-worker controlled endpoint is a HostApp console service and does not expose an HTTP pipeline, so this stays empty and avoids adding the WebApp entry standard.</para>
    /// </summary>
    /// <param name="app">
    /// <para>zh-cn:应用管道构建器，HostApp 场景不使用。</para>
    /// <para>en-us:Application pipeline builder, unused for HostApp.</para>
    /// </param>
    /// <param name="env">
    /// <para>zh-cn:宿主环境，HostApp 场景不使用。</para>
    /// <para>en-us:Host environment, unused for HostApp.</para>
    /// </param>
    public override void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
    }
}
