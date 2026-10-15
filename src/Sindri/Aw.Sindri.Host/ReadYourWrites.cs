using System.Globalization;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host;

/// <summary>
/// Read-your-writes on queries (API spec §2.4, plan G6): a query endpoint (one carrying <see cref="QueryRouteMetadata"/>)
/// with an X-Sindri-After header waits until the views reach that sequence. A malformed value is 400 SND-CLI-003; views
/// still behind after 2 s (or stalled behind it) are 503 SND-API-011, naming the cursor and the target.
/// </summary>
internal static class ReadYourWrites
{
    /// <summary>How long a query waits for the views.</summary>
    internal static readonly TimeSpan Limit = TimeSpan.FromSeconds(2);

    internal static WebApplication UseReadYourWrites(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue(SindriHeaders.After, out var after)
                && context.GetEndpoint()?.Metadata.GetMetadata<QueryRouteMetadata>() is not null)
            {
                if (!long.TryParse(after.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
                {
                    await ApiProblems.Write(context, SindriErrors.InvalidInput.Code, StatusCodes.Status400BadRequest,
                        $"{SindriHeaders.After} '{after}' isn't a ledger sequence (fix: send a whole number, the Sequence of a command's answer)").ConfigureAwait(false);
                    return;
                }

                var reached = await context.RequestServices.GetRequiredService<IProjectorStatus>()
                    .WaitFor(sequence, Limit, context.RequestAborted).ConfigureAwait(false);
                if (reached.IsError)
                {
                    await ApiProblems.Write(context, reached.Error, StatusCodes.Status503ServiceUnavailable).ConfigureAwait(false);
                    return;
                }
            }

            await next(context).ConfigureAwait(false);
        });
        return app;
    }
}
