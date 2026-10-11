using Vogen;

namespace Aw.Sindri.Host;

/// <summary>Maps Vogen's validation exception (an uninitialized id from an empty route or query value) to 400.</summary>
internal static class ValueObjectValidation
{
    internal static WebApplication UseValueObjectValidation(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (ValueObjectValidationException) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        });
        return app;
    }
}
