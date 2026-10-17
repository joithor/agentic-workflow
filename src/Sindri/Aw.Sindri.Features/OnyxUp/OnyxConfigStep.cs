using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 3: the .env and the compose override (stamped by the fetched files, port, platform, release and this platform's image digests). A present auth key is always kept;
// a missing one is generated once. Both files are rendered on every run and written only when they differ, so an edited or deleted file is put back.
internal sealed class OnyxConfigStep(IOnyxWorkspace workspace, IComposeServices services, IRandomHex random, IOnyxProgress progress) : IOnyxUpStep
{
    private const int AuthKeyBytes = 32;

    public string Name => OnyxSteps.Config;

    public string? After => OnyxSteps.Fetch;

    public string Stamp(OnyxUpContext context)
        => OnyxSteps.Stamp(context.Upstream(OnyxSteps.Fetch), context.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), context.Platform, context.Pins.Version, ImageRows(context));

    // The override names every image by its digest, so the digests this platform runs are inputs of the step (a moved pin re-renders, re-pulls and restarts).
    private static string ImageRows(OnyxUpContext context)
        => string.Join('\n', context.Pins.Images.Where(i => i.Platform == context.Platform).Select(static i => i.Repository + "@" + i.Digest).Order(StringComparer.Ordinal));

    public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string? recorded, CancellationToken cancellationToken)
    {
        var compose = workspace.Read(context.Paths.Compose);
        var template = workspace.Read(context.Paths.EnvTemplate);
        if (compose is null || template is null)
        {
            return Done(SindriErrors.OnyxPinsMalformed.With("the fetched compose file or env.template is missing; run `sindri onyx up` again to fetch them"));
        }

        var existing = OnyxConfig.ReadSecret(workspace.Read(context.Paths.Env));
        var key = existing ?? random.Hex(AuthKeyBytes);
        var settings = context.Settings(key);
        var rendered = services.Read(compose).Bind(list => OnyxConfig.RenderOverride(list, settings, context.Pins, context.Platform));
        if (rendered.IsError)
        {
            return Done(rendered.Error);
        }

        var env = OnyxConfig.RenderEnv(template, settings);
        var stale = !string.Equals(recorded, Stamp(context), StringComparison.Ordinal)
            || !string.Equals(workspace.Read(context.Paths.Env), env, StringComparison.Ordinal)
            || !string.Equals(workspace.Read(context.Paths.Override), rendered.Value, StringComparison.Ordinal);
        if (stale)
        {
            progress.Line($"{Name}: running");
        }

        var wrote = workspace.WriteIfChanged(context.Paths.Env, env, secret: true);
        wrote |= workspace.WriteIfChanged(context.Paths.Override, rendered.Value, secret: false);
        return Done(stale || wrote ? OnyxStepOutcome.Ran : OnyxStepOutcome.Skipped);
    }

    private static ValueTask<Result<OnyxStepOutcome>> Done(Result<OnyxStepOutcome> result) => ValueTask.FromResult(result);
}
