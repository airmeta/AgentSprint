using Microsoft.Extensions.Hosting;

namespace AgentSprint.Worker.Actors;

internal sealed class WorkerActorDependencyInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public WorkerActorDependencyInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        WorkerActorDependencyAccessor.Use(_serviceProvider);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        WorkerActorDependencyAccessor.Clear(_serviceProvider);
        return Task.CompletedTask;
    }
}
