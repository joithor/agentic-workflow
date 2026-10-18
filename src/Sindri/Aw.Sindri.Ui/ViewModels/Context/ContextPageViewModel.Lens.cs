using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The lens stack (U9a): the anchor box's read of GET /sindri/context/lenses and what it draws, read-only.</summary>
internal sealed partial class ContextPageViewModel
{
    private const int MaxAnchors = 20;
    private int _generation;

    /// <summary>The three bands, in order: intent, product, code. Empty before a read.</summary>
    public IReadOnlyList<ContextBandViewModel> Bands { get; private set; } = [];

    /// <summary>The edges between the nodes on screen.</summary>
    public IReadOnlyList<ContextLine> Edges { get; private set; } = [];

    /// <summary>The coverage strip's bars.</summary>
    public IReadOnlyList<ContextLine> Coverage { get; private set; } = [];

    /// <summary>The searched sources, one chip each.</summary>
    public IReadOnlyList<ContextLine> Searched { get; private set; } = [];

    /// <summary>The anchors the map does not hold, each as "not found".</summary>
    public IReadOnlyList<ContextLine> Missing { get; private set; } = [];

    /// <summary>The served suggestions after a refusal, as choices (the anchors to pick).</summary>
    public IReadOnlyList<string> Choices { get; private set; } = [];

    /// <summary>The refusal's own words, or empty.</summary>
    public string Problem { get; private set; } = string.Empty;

    /// <summary>Whether a stack is drawn.</summary>
    public bool HasStack => Bands.Count > 0;

    /// <summary>Whether the refusal offers choices.</summary>
    public bool HasChoices => Choices.Count > 0;

    /// <summary>Whether there is a refusal to show.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>The edges list's header.</summary>
    public string EdgesHeader => Labels.ContextBandHeader(Labels.ContextEdges, Edges.Count);

    /// <summary>The coverage strip's name.</summary>
    public string CoverageName { get; } = Labels.ContextCoverage;

    /// <summary>The searched sources' name.</summary>
    public string SearchedName { get; } = Labels.ContextSearched;

    /// <summary>The choices' name.</summary>
    public string ChoicesName { get; } = Labels.ContextSuggestions;

    /// <summary>Reads the lenses for the anchors in the box (Enter): one read, the newest answer wins.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    public async Task Submit(CancellationToken cancellationToken = default)
    {
        var anchors = AnchorText.Split([' ', ',', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();
        if (anchors.Count == 0 || Ports.ApiUrl() is not { } api)
        {
            return;
        }

        if (anchors.Count > MaxAnchors)
        {
            Show(view: null, choices: []);
            Problem = Labels.ContextTooMany(MaxAnchors);
            Refresh();
            return;
        }

        var generation = ++_generation;
        var result = await Ports.Api.Lenses(api, new LensesQuery(anchors, Depth: null, Draft: false, MapVersion: null), cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }

        if (result.IsSuccess)
        {
            Show(result.Value, choices: []);
        }
        else
        {
            Show(view: null, choices: Suggestions(result.Error));
            Problem = result.Error.Message;
        }

        Refresh();
    }

    /// <summary>Picks a suggested anchor: it replaces the box and is read.</summary>
    /// <param name="anchor">The suggested anchor.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    public Task Pick(string anchor, CancellationToken cancellationToken = default)
    {
        AnchorText = anchor;
        return Submit(cancellationToken);
    }

    private static List<string> Suggestions(Error error)
    {
        var name = string.Equals(error.Code, "SND-CONTEXT-003", StringComparison.Ordinal) ? "suggestedAnchors"
            : string.Equals(error.Code, "SND-CONTEXT-037", StringComparison.Ordinal) ? "candidates"
            : null;
        if (name is null || error.Extensions is null)
        {
            return [];
        }

        return [.. error.Extensions.Where(m => string.Equals(m.Key, name, StringComparison.Ordinal) && m.Value.ValueKind == JsonValueKind.Array)
            .SelectMany(m => m.Value.EnumerateArray()).Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString() ?? string.Empty)];
    }

    private void Show(LensesView? view, List<string> choices)
    {
        Problem = string.Empty;
        Choices = choices;
        if (view is null)
        {
            Head = string.Empty;
            Bands = [];
            Edges = [];
            Coverage = [];
            Searched = [];
            Missing = [];
            return;
        }

        var version = view.MapCommit is { Length: > 0 } commit ? $"{view.MapVersion}@{commit}" : view.MapVersion;
        Head = Labels.ContextHead(version, view.Anchors.Count) + (view.Draft ? " · " + Labels.ContextDraftMark : string.Empty);
        var names = Names(view);
        Bands = [Intent(view), Product(view), Code(view)];
        Edges = EdgeLines(view, names);
        Coverage = CoverageLines(view.Coverage);
        Searched = [.. view.Searched.Sources.Select(s => new ContextLine(string.Join(" · ", new[] { s.Id, s.State, s.Reason ?? s.Code }.Where(w => !string.IsNullOrEmpty(w)))))];
        Missing = [.. (view.MissingAnchors ?? []).Select(a => new ContextLine($"{a} · {Labels.ContextNotFound}"))];
    }

    private void Refresh()
    {
        OnChanged(nameof(Head));
        OnChanged(nameof(HasHead));
        OnChanged(nameof(Bands));
        OnChanged(nameof(Edges));
        OnChanged(nameof(EdgesHeader));
        OnChanged(nameof(Coverage));
        OnChanged(nameof(Searched));
        OnChanged(nameof(Missing));
        OnChanged(nameof(Choices));
        OnChanged(nameof(Problem));
        OnChanged(nameof(HasStack));
        OnChanged(nameof(HasChoices));
        OnChanged(nameof(HasProblem));
    }

    private static Dictionary<string, string> Names(LensesView view)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in view.Anchors)
        {
            names[a.Key] = a.Label;
        }

        foreach (var r in view.Bands.Intent.Records)
        {
            names[r.Key] = r.Label;
        }

        foreach (var d in view.Bands.Intent.Decisions)
        {
            names[d.Node] = d.Label;
        }

        foreach (var n in view.Bands.Product.Own.Concat(view.Bands.Product.Neighbors).Concat(view.Bands.Product.Affects))
        {
            names[n.Node] = n.Label;
        }

        foreach (var f in view.Bands.Code.Files)
        {
            names[CodeKey(f)] = f.Path;
        }

        return names;
    }

    private static string CodeKey(CodeFile file) => "code:" + file.Path;

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string Join(params string?[] words) => string.Join(" · ", words.Where(w => !string.IsNullOrEmpty(w)));

    private static ContextBandViewModel Intent(LensesView view)
    {
        var intent = view.Bands.Intent;
        List<ContextLine> lines =
        [
            .. intent.Records.Select(r => new ContextLine(Join(r.Label, r.Source, Untrusted(r.Trust)))),
            .. intent.Decisions.Select(d => new ContextLine(Join(d.Label, d.At.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), d.Confirmed ? null : Labels.ContextUnconfirmed, Untrusted(d.Trust)))),
            .. view.Conflicts.Select(c => new ContextLine(Join(Labels.ContextConflict, c.Kind, c.Node, c.Summary))),
        ];
        if (intent.Truncated)
        {
            lines.Add(new ContextLine(Labels.ContextTruncated));
        }

        return new ContextBandViewModel(Labels.ContextBandHeader(Labels.ContextIntent, intent.Records.Count + intent.Decisions.Count), lines);
    }

    private static string? Untrusted(string trust) => string.Equals(trust, "untrusted", StringComparison.Ordinal) ? Labels.ContextUntrusted : null;

    private static ContextBandViewModel Product(LensesView view)
    {
        var product = view.Bands.Product;
        var anchors = view.Anchors.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        ContextLine Row(ProductNode n, string? list) => new(Join(n.Label, n.Kind, anchors.Contains(n.Node) ? Labels.ContextAnchorTag : null, list,
            n.Depth is { } depth && depth > 0 ? Inv($"depth {depth}") : null, n.Confirmed ? null : Labels.ContextUnconfirmed, Untrusted(n.Trust)));
        List<ContextLine> lines =
        [
            .. product.Own.Select(n => Row(n, list: null)),
            .. product.Neighbors.Select(n => Row(n, "neighbor")),
            .. product.Affects.Select(n => Row(n, "affects")),
        ];
        if (product.Truncated)
        {
            lines.Add(new ContextLine(Labels.ContextTruncated));
        }

        return new ContextBandViewModel(Labels.ContextBandHeader(Labels.ContextProduct, lines.Count), lines);
    }

    private static ContextBandViewModel Code(LensesView view)
    {
        var code = view.Bands.Code;
        List<ContextLine> lines =
        [
            .. code.Files.Select(f => new ContextLine(Join(f.Path, f.Suggested ? Inv($"{Labels.ContextSuggestedFile} {f.Confidence:0.00}") : null, f.Implements.Count > 0 ? "implements " + string.Join(", ", f.Implements) : null))),
            new ContextLine(Join(Labels.ContextSymbols, string.Equals(code.Symbols.State, "unavailable", StringComparison.Ordinal) ? Labels.ContextIndexNotBuilt : code.Symbols.State)),
        ];
        if (code.Truncated)
        {
            lines.Add(new ContextLine(Labels.ContextTruncated));
        }

        return new ContextBandViewModel(Labels.ContextBandHeader(Labels.ContextCode, code.Files.Count), lines);
    }

    private static List<ContextLine> EdgeLines(LensesView view, Dictionary<string, string> names) =>
        [.. view.Bands.Product.Edges.Concat(view.Edges)
            .Where(e => names.ContainsKey(e.From) && names.ContainsKey(e.To))
            .DistinctBy(e => (e.From, e.To, e.Type))
            .Select(e => new ContextLine(Join($"{names[e.From]} {e.Type} {names[e.To]}",
                string.Equals(e.Provenance, "model", StringComparison.Ordinal) && !e.Confirmed ? Inv($"{Labels.ContextModelSuggested} {e.Confidence:0.00}") : null)))];

    private static List<ContextLine> CoverageLines(CoverageView c) =>
    [
        new(Labels.ContextCoverIntent(c.Intent.Covered, c.Intent.Of)),
        new(Labels.ContextCoverProduct(c.Product.Confirmed, c.Product.Of)),
        new(Labels.ContextCoverCode(c.Code.Implemented, c.Code.Of, c.Code.Suggested)),
    ];
}
