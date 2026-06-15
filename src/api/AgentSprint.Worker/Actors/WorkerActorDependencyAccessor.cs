using Microsoft.Extensions.DependencyInjection;

namespace AgentSprint.Worker.Actors;

internal static class WorkerActorDependencyAccessor
{
    private static IServiceProvider? _serviceProvider;

    public static void Use(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static void Clear(IServiceProvider serviceProvider)
    {
        if (ReferenceEquals(_serviceProvider, serviceProvider))
        {
            _serviceProvider = null;
        }
    }

    public static T GetRequiredService<T>() where T : notnull
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("Worker actor dependencies have not been initialized.");
        }

        return _serviceProvider.GetRequiredService<T>();
    }
}
