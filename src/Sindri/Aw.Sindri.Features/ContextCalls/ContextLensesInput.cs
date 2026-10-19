using System.Text.Json;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// 'input.context.lenses' (scoping spec C1, W6): the product band, the cascade, 'searched' and the map coverage for the anchors, read in process from
/// the engine's multi-anchor lenses as the service, cut to the instance's repo. The section 'lenses' is the readable text; the typed result is the
/// <see cref="LensesSetView"/> as the route answers it. A call the engine refuses, or an instance with no mapped repo, is an honest section saying
/// so (and no typed result): the Step still runs and the gather records the gap.
/// </summary>
/// <param name="lenses">The context engine (its facade for this lens).</param>
/// <param name="scopes">The service scope for the instance's repo.</param>
/// <param name="facts">Reads the content behind a fact-reference argument.</param>
/// <param name="json">The host's JSON options.</param>
internal sealed class ContextLensesInput(IContextEngine lenses, ContextInputScopes scopes, ContextInputFacts facts, JsonSerializerOptions json) : IStepInput, IDescribedBlock
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
    public async ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var notes = new List<string>();
        var resolved = await facts.Resolve(context, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            if (ContextInputFacts.Fails(resolved.Error))
            {
                return resolved.Error;
            }

            return Unavailable(resolved.Error.Code + ": " + resolved.Error.Message, notes);
        }

        context = resolved.Value;
        var anchors = ContextInputArguments.Anchors(context, notes);
        if (anchors.Count == 0)
        {
            return Unavailable("this Step named no anchors", notes);
        }

        var depth = Depth(context, notes);
        var hops = Hops(context, notes);
        var scope = await scopes.For(context.Repo, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return Unavailable(scope.Error.Code + ": " + scope.Error.Message, notes);
        }

        var view = await lenses.LensesOf(anchors, MapRead.Current, new LensRange(depth, hops), scope.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return Unavailable(view.Error.Code + ": " + view.Error.Message, notes);
        }

        var stable = ContextInputSections.Stable(view.Value);
        return ContextInputSections.Filled(Section, ContextLensText.LensesSet(stable, fence: false), notes, stable, json);
    }

    private static InjectionSections Unavailable(string why, List<string> notes) => ContextInputSections.Unavailable(Section, "context lenses", why, notes);

    private static int? Depth(StepInputContext context, List<string> notes)
    {
        if (!context.Arguments.TryGetValue("depth", out var depth))
        {
            return null;
        }

        if (depth.ValueKind == JsonValueKind.Number && depth.TryGetInt32(out var n))
        {
            return n;
        }

        notes.Add("depth is not a whole number, so the profile's cascade depth was used");
        return null;
    }

    private static string? Hops(StepInputContext context, List<string> notes)
    {
        if (!context.Arguments.TryGetValue("hops", out var hops))
        {
            return null;
        }

        if (hops.ValueKind != JsonValueKind.Array)
        {
            notes.Add("hops is not a list, so the default hops were used");
            return null;
        }

        var items = hops.EnumerateArray().ToList();
        var names = items.Where(static h => h.ValueKind == JsonValueKind.String).Select(static h => h.GetString()).ToList();
        if (names.Count < items.Count)
        {
            notes.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{items.Count - names.Count} of {items.Count} hops are not text and were ignored"));
        }

        return names.Count == 0 ? null : string.Join(',', names);
    }
}
