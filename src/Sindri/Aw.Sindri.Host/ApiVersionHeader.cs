using System.Globalization;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Host;

/// <summary>
/// Adds Sindri-Api-Version to every answer after the loopback guard (API spec §9). It is set when the response starts,
/// so a ProblemDetails answer (which clears the response first) carries it too.
/// </summary>
internal static class ApiVersionHeader
{
    private static readonly string _version = SindriApi.Version.ToString(CultureInfo.InvariantCulture);

    internal static WebApplication UseApiVersionHeader(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                ((HttpResponse)state).Headers[SindriHeaders.ApiVersion] = _version;
                return Task.CompletedTask;
            }, context.Response);
            await next(context).ConfigureAwait(false);
        });
        return app;
    }
}
