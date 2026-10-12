using Aw.Kernel.Ids;
using Aw.Sindri.Core.Commands;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>Interim (see <see cref="InterimCallers"/>): every request runs as the human CLI at a terminal.</summary>
internal sealed class InterimHumanRequests : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        _ = app.Use(async (context, call) =>
        {
            var services = context.RequestServices;
            using var entered = services.GetRequiredService<ICallerContextAccessor>().Enter(
                CallerContext.Http(CallerClass.Human, terminal: true, ClientLabel.Cli, repo: null, services.GetRequiredService<IIdGenerator>()));
            await call(context);
        });
        next(app);
    };
}
