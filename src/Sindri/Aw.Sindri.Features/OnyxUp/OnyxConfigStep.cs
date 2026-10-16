using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 3: the .env and the compose override (stamped by the fetched files, port, platform, release and this platform's image digests). A present auth key is always kept; a missing one is generated once.
internal sealed class OnyxConfigStep(IOnyxWorkspace workspace, IComposeServices services, IRandomHex random) : IOnyxUpStep
{
    private const int AuthKeyBytes = 32;

    public string Name => OnyxSteps.Config;

    public string Stamp(OnyxUpContext context)
        => OnyxSteps.Stamp(context.Done.GetValueOrDefault(OnyxSteps.Fetch, ""), context.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), context.Platform, context.Pins.Version, ImageRows(context));

    // The override names every image by its digest, so the digests this platform runs are inputs of the step (a moved pin re-renders, re-pulls and restarts).
    private static string ImageRows(OnyxUpContext context)
        => string.Join('\n', context.Pins.Images.Where(i => i.Platform == context.Platform).Select(static i => i.Repository + "@" + i.Digest).Order(StringComparer.Ordinal));

    public ValueTask<bool> IsDone(OnyxUpContext context, string? recorded, CancellationToken cancellationToken)
        => ValueTask.FromResult(
            string.Equals(recorded, Stamp(context), StringComparison.Ordinal)
            && workspace.Read(context.Paths.Override) is not null
            && OnyxConfig.ReadSecret(workspace.Read(context.Paths.Env)) is not null);

    public ValueTask<Result<Unit>> Run(OnyxUpContext context, CancellationToken cancellationToken)
    {
        var compose = workspace.Read(context.Paths.Compose);
        var template = workspace.Read(context.Paths.EnvTemplate);
        if (compose is null || template is null)
        {
            return ValueTask.FromResult<Result<Unit>>(SindriErrors.OnyxPinsMalformed.With("the fetched compose file or env.template is missing; run `sindri onyx up` again to fetch them"));
        }

        var key = OnyxConfig.ReadSecret(workspace.Read(context.Paths.Env)) ?? random.Hex(AuthKeyBytes);
        var settings = context.Settings(key);
        var rendered = services.Read(compose).Bind(list => OnyxConfig.RenderOverride(list, settings, context.Pins, context.Platform));
        if (rendered.IsError)
        {
            return ValueTask.FromResult<Result<Unit>>(rendered.Error);
        }

        _ = workspace.WriteIfChanged(context.Paths.Env, OnyxConfig.RenderEnv(template, settings), secret: true);
        _ = workspace.WriteIfChanged(context.Paths.Override, rendered.Value, secret: false);
        return ValueTask.FromResult(Results.Ok());
    }
}
