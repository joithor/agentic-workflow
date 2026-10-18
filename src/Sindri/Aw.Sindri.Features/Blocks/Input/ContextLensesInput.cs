using System.Text.Json;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Features.Blocks.Input;

/// <summary>
/// 'input.context.lenses' (scoping spec C1, W6): the product band, the cascade, 'searched' and the map coverage for the anchors, read in process from
/// the engine's multi-anchor lenses as the service, cut to the instance's repo. The section 'lenses' is the readable text; the typed result is the
/// <see cref="LensesSetView"/> as the route answers it. A call the engine refuses, or an instance with no mapped repo, is an honest section saying
/// so (and no typed result): the Step still runs and the gather records the gap.
/// </summary>
/// <param name="lenses">The engine's lenses.</param>
/// <param name="scopes">The service scope for the instance's repo.</param>
/// <param name="json">The host's JSON options.</param>
internal sealed class ContextLensesInput(IContextLenses lenses, ContextInputScopes scopes, JsonSerializerOptions json) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.context.lenses";

    /// <summary>The section the text is under.</summary>
    public const string Section = "lenses";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From(Id), BlockKind.StepInput, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.Tools,
        [
            new ParameterSpec("anchors", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("depth", ParameterType.Number, Required: false, IsCapabilityField: false),
            new ParameterSpec("hops", ParameterType.List, Required: false, IsCapabilityField: false),
        ],
        Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public async ValueTask<InjectionSections> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var anchors = ContextInputArguments.Anchors(context);
        if (anchors.Count == 0)
        {
            return Unavailable("this Step named no anchors");
        }

        var scope = await scopes.For(context.Repo, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return Unavailable(scope.Error.Code + ": " + scope.Error.Message);
        }

        var range = new LensRange(Depth(context), Hops(context));
        var view = await lenses.LensesOf(anchors, MapRead.Current, range, scope.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return Unavailable(view.Error.Code + ": " + view.Error.Message);
        }

        var text = ContextLensText.LensesSet(view.Value, fence: true);
        return new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { [Section] = text }, JsonSerializer.SerializeToElement(view.Value, json.GetTypeInfo(typeof(LensesSetView))));
    }

    private static InjectionSections Unavailable(string why) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal) { [Section] = "The context lenses are unavailable for this Step (" + why + "); it runs without them.\n" });

    private static int? Depth(StepInputContext context) =>
        context.Arguments.TryGetValue("depth", out var depth) && depth.ValueKind == JsonValueKind.Number && depth.TryGetInt32(out var n) ? n : null;

    private static string? Hops(StepInputContext context) =>
        context.Arguments.TryGetValue("hops", out var hops) && hops.ValueKind == JsonValueKind.Array
            ? string.Join(',', hops.EnumerateArray().Where(static h => h.ValueKind == JsonValueKind.String).Select(static h => h.GetString()))
            : null;
}
