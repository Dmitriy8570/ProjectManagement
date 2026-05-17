using System.Reflection;
using Microsoft.Extensions.Hosting;

// Lives in the Microsoft.Extensions.DependencyInjection namespace so callers
// can pick it up off IHostApplicationBuilder without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR with all command/query handlers in this assembly.
    /// </summary>
    public static void AddBusinessServices(this IHostApplicationBuilder builder)
    {
        var assembly = Assembly.GetExecutingAssembly();
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
    }
}
