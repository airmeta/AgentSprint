using System.Reflection;

using Air.Cloud.HostApp.Dependency;

using Microsoft.Extensions.Hosting;

var hostBuilder = Host.CreateDefaultBuilder(args);
hostBuilder.HostInjectInFile(Assembly.GetExecutingAssembly());

var host = hostBuilder.Build();
await host.RunAsync();
