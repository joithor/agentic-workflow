using Aw.Kernel.Ids;
using Aw.Sindri.Core.Commands;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// Interim, until Unit B lands (API P1 plan §3 Unit A, "after rebasing on B"): Unit B's middleware turns the caller
/// headers into a caller context and ServeCommand enters 'service:host' for its own stop calls. Until then these S1 host
/// tests get their callers here: a human CLI at a terminal around every request, and 'service:host' when nothing else
/// is current. When rebasing on B, delete this (and its two helpers) and send <see cref="Callers.AsHuman"/> instead.
/// </summary>
internal static class InterimCallers
{
    public static void Configure(IServiceCollection services)
    {
        _ = services.AddSingleton<IStartupFilter, InterimHumanRequests>();
        _ = services.AddSingleton<ICallerContextAccessor>(static sp =>
            new InterimHostCaller(new AsyncLocalCallerContextAccessor(sp.GetRequiredService<IIdGenerator>()), sp.GetRequiredService<IIdGenerator>()));
    }
}
