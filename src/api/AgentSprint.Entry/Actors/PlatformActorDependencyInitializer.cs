namespace AgentSprint.Entry.Actors;

internal sealed class PlatformActorDependencyInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// <para>zh-cn: 创建平台端 Actor 依赖初始化器，在 Akka Actor 启动前保存根服务容器，供无参 Actor 解析业务服务。</para>
    /// <para>en-us: Creates the platform actor dependency initializer, storing the root service provider before Akka actors start so parameterless actors can resolve business services.</para>
    /// </summary>
    public PlatformActorDependencyInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// <para>zh-cn: 启动时登记服务容器；该方法无阻塞副作用，Actor 收到消息后才实际解析业务服务。</para>
    /// <para>en-us: Registers the service provider on startup; this method has no blocking side effects, and actors resolve business services when messages arrive.</para>
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        PlatformActorDependencyAccessor.Use(_serviceProvider);
        return Task.CompletedTask;
    }

    /// <summary>
    /// <para>zh-cn: 停止时清理服务容器引用，避免宿主重启或测试释放后 Actor 继续持有旧容器。</para>
    /// <para>en-us: Clears the service-provider reference on shutdown so actors do not hold an old container after host restart or test disposal.</para>
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        PlatformActorDependencyAccessor.Clear(_serviceProvider);
        return Task.CompletedTask;
    }
}

