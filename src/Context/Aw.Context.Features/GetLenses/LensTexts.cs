using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.GetLenses;

/// <summary>Cleans every piece of map text in a lenses view the way every answer's is cleaned (spec §7): scrubbed, one line for a label, fenced when the caller's policy fences untrusted text.</summary>
internal static class LensTexts
{
    internal static LensesView Clean(LensesView view, MapText text) => view with
    {
        Anchor = view.Anchor with { Label = text.Label(view.Anchor.Label, view.Anchor.Trust) },
        Bands = Bands(view.Bands, text),
        SkippedComponents = Units(view.SkippedComponents, text),
    };

    internal static LensesSetView Clean(LensesSetView view, MapText text) => view with
    {
        Anchors = [.. view.Anchors.Select(a => a with { Label = text.Label(a.Label, a.Trust) })],
        MissingAnchors = [.. view.MissingAnchors.Select(text.Line)],
        Bands = Bands(view.Bands, text),
        SkippedComponents = Units(view.SkippedComponents, text),
    };

    private static IReadOnlyList<SkippedComponent>? Units(IReadOnlyList<SkippedComponent>? units, MapText text) => units is null ? null : [.. units.Select(u => u with { Unit = text.Line(u.Unit) })];

    private static LensBands Bands(LensBands bands, MapText text) => new(Intent(bands.Intent, text), Product(bands.Product, text), Code(bands.Code, text));

    private static IntentBand Intent(IntentBand band, MapText text) => band with
    {
        Records = [.. band.Records.Select(r => r with { Label = text.Label(r.Label, r.Trust) })],
        Decisions = [.. band.Decisions.Select(d => d with
        {
            Label = text.Label(d.Label, d.Trust),
            Text = text.Body(d.Text, d.Trust),
            Quote = d.Quote is { } q ? q with { Text = text.Body(q.Text, q.Trust) } : null,
        })],
    };

    private static ProductBand Product(ProductBand band, MapText text) => band with
    {
        Own = [.. band.Own.Select(i => i with { Label = text.Label(i.Label, i.Trust) })],
        Neighbors = [.. band.Neighbors.Select(r => Reach(r, text))],
        Affects = [.. band.Affects.Select(r => Reach(r, text))],
    };

    private static LensReach Reach(LensReach reach, MapText text) => reach with { Label = text.Label(reach.Label, reach.Trust), Path = [.. reach.Path.Select(text.Line)] };

    private static CodeBand Code(CodeBand band, MapText text) => band with { Files = [.. band.Files.Select(f => f with { Path = text.Line(f.Path), Label = text.Line(f.Path) })] };
}
