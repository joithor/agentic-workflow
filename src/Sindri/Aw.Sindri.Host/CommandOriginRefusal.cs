using Aw.Sindri.Core;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host;

/// <summary>
/// Refuses any Origin header on a command endpoint (one carrying <see cref="CommandRouteMetadata"/>) or a body query (<see cref="BodyQueryRouteMetadata"/>) with 403 SND-API-008
/// (API spec §6). No first-party client sends one, so this stops a page in a local browser driving a command. It runs as
/// middleware after routing and before the endpoint, so a malformed body can't turn the refusal into a 400 (plan G21).
/// Queries keep S1's loopback guard (C3). The refusal is logged, not audited (plan G13).
/// </summary>
internal static partial class CommandOriginRefusal
{
    internal static WebApplication UseCommandOriginRefusal(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            var metadata = context.GetEndpoint()?.Metadata;
            var isBodyQuery = metadata?.GetMetadata<BodyQueryRouteMetadata>() is not null;
            if (context.Request.Headers.ContainsKey("Origin") && (isBodyQuery || metadata?.GetMetadata<CommandRouteMetadata>() is not null))
            {
                var path = context.Request.Path.Value ?? string.Empty;
                LogRefused(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CommandOriginRefusal)),
                    context.Request.Method, path, context.Request.Headers.Origin.ToString());
                await ApiProblems.Write(context, SindriErrors.OriginRefused.Code, StatusCodes.Status403Forbidden,
                    isBodyQuery ? $"{context.Request.Method} {path}: a body query from a foreign Origin is refused (fix: call it from the sindri CLI or the Sindri app)"
                        : $"{context.Request.Method} {path} is a command, and commands refuse an Origin header (fix: call it from the sindri CLI or the Sindri app)").ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });
        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-API-008 {Method} {Path} refused: Origin '{Origin}' on a command or body query")]
    private static partial void LogRefused(ILogger logger, string method, string path, string origin);
}
