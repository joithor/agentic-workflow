using System.Text.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>
/// A scope-map fact (the demo slice of U13): a counts line, a row per surface and short sections, read tolerantly from the body as a JSON
/// tree because two shapes exist (the API golden and the scoping schema's fact). A body with no surfaces array, and the Show raw
/// toggle, show the generic payload tree.
/// </summary>
internal sealed class ScopeMapViewer : ObservableObject, IFactViewer
{
    private static readonly string[] _headKeys = ["title", "item", "name", "path", "id", "rule"];
    private static readonly string[] _whyKeys = ["reason", "why", "detail"];

    private readonly FactViewerContext _context;

    internal ScopeMapViewer(FactRow row, FactViewerContext context)
    {
        (Row, _context) = (row, context);
    }

    /// <summary>The fact row the viewer shows.</summary>
    public FactRow Row { get; }

    /// <summary>The polite line that says the outcome of the read.</summary>
    public Components.LiveAnnouncer Voice { get; } = new(Labels.ReadingFact);

    /// <summary>True until the body has been read.</summary>
    public bool IsLoading { get; private set { Set(ref field, value); Refresh(); } } = true;

    /// <summary>The words while the body is read.</summary>
#pragma warning disable CA1822, MA0041 // a fixed sentence is a property because the view binds it
    public string LoadingText => Labels.ReadingFact;
#pragma warning restore CA1822, MA0041

    /// <summary>The served sentence when the body can't be shown; empty otherwise.</summary>
    public string ProblemText { get; private set { Set(ref field, value); OnChanged(nameof(HasProblem)); Refresh(); } } = string.Empty;

    /// <summary>True when the body can't be shown.</summary>
    public bool HasProblem => ProblemText.Length > 0;

    /// <summary>True when the body has a surfaces array to draw as a table.</summary>
    public bool HasSurfaces { get; private set { Set(ref field, value); Refresh(); } }

    /// <summary>True while the raw tree is shown instead of the table.</summary>
    public bool IsRaw { get; private set { Set(ref field, value); Refresh(); OnChanged(nameof(ToggleLabel)); } }

    /// <summary>True when the table is shown.</summary>
    public bool ShowsTable => !IsLoading && !HasProblem && HasSurfaces && !IsRaw;

    /// <summary>True when the generic tree is shown.</summary>
    public bool ShowsTree => !IsLoading && !HasProblem && (!HasSurfaces || IsRaw);

    /// <summary>The toggle's words: Show raw, or Show table.</summary>
    public string ToggleLabel => IsRaw ? Labels.ShowTable : Labels.ShowRaw;

    /// <summary>The table's accessible name.</summary>
    public string TableName => Labels.ScopeTableName(Row.Label);

    /// <summary>The counts line: "4 surfaces · 2 mapped · 1 new · 1 unmapped".</summary>
    public string Counts { get; private set; } = string.Empty;

    /// <summary>One row per surface.</summary>
    public IReadOnlyList<ScopeSurfaceRow> Surfaces { get; private set; } = [];

    /// <summary>The sections that the body has.</summary>
    public IReadOnlyList<ScopeSection> Sections { get; private set; } = [];

    /// <summary>The whole body as a tree (the fallback and the raw view).</summary>
    public IReadOnlyList<PayloadNodeViewModel> Tree { get; private set; } = [];

    /// <summary>The engine's truncation, in words; empty when the body is whole.</summary>
    public string TruncatedNote { get; private set { Set(ref field, value); OnChanged(nameof(HasTruncatedNote)); } } = string.Empty;

    /// <summary>True when the body was cut short.</summary>
    public bool HasTruncatedNote => TruncatedNote.Length > 0;

    /// <summary>Switches between the table and the raw tree.</summary>
    internal void ToggleRaw() => IsRaw = !IsRaw;

    /// <summary>Reads the fact's route.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the viewer shows the answer.</returns>
    public async Task Load(CancellationToken cancellationToken)
    {
        if (_context.ApiUrl() is not { } url)
        {
            ProblemText = Labels.PayloadNotRunning;
            await Task.Yield();   // the live region is created with the viewer: its first words are said a turn later, so the change is spoken
            Voice.Say(ProblemText);
            IsLoading = false;
            return;
        }

        var answer = await _context.Api.InstanceFact(url, _context.InstanceId, Row.Key, cancellationToken).ConfigureAwait(true);
        if (answer.IsError)
        {
            ProblemText = string.Equals(answer.Error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal) ? Labels.StripUnreadable : answer.Error.Message;
            Voice.Say(ProblemText);
        }
        else
        {
            Show(answer.Value);
            Voice.Say(TruncatedNote.Length > 0 ? TruncatedNote : HasSurfaces ? Labels.ScopeLoaded(Row.Label, Counts) : Labels.PayloadLoaded(Row.Label));
        }

        IsLoading = false;
    }

    private void Show(FactView fact)
    {
        Tree = PayloadNodeViewModel.Roots(fact.Body);
        TruncatedNote = fact.Truncated ? Labels.PayloadTruncated : string.Empty;
        if (fact.Body.ValueKind == JsonValueKind.Object && fact.Body.TryGetProperty("surfaces", out var surfaces) && surfaces.ValueKind == JsonValueKind.Array)
        {
            Surfaces = [.. surfaces.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.Object).Select(Surface)];
            Counts = CountsLine(Surfaces);
            Sections = Sectioned(fact.Body);
            HasSurfaces = true;
        }
    }

    private void Refresh()
    {
        OnChanged(nameof(ShowsTable));
        OnChanged(nameof(ShowsTree));
    }

    private static ScopeSurfaceRow Surface(JsonElement surface)
    {
        var title = Text(surface, "title") is { Length: > 0 } t ? t : Text(surface, "name") is { Length: > 0 } n ? n : Text(surface, "id") is { Length: > 0 } i ? i : Labels.ScopeUnnamed;
        var meta = string.Join(" · ", new[] { Text(surface, "id"), Text(surface, "kind"), Text(surface, "lens") }.Where(s => s.Length > 0 && !string.Equals(s, title, StringComparison.Ordinal)));
        var graph = Text(surface, "graph") is { Length: > 0 } g ? g : Text(surface, "graphStatus");
        var count = Length(surface, "files") is { } files ? Labels.ScopeFiles(files) : Length(surface, "citations") is { } cites ? Labels.ScopeCitations(cites) : string.Empty;
        return new ScopeSurfaceRow(title, meta, Text(surface, "detail"), graph, count);
    }

    private static string CountsLine(IReadOnlyList<ScopeSurfaceRow> rows)
    {
        var words = rows.Select(r => r.Graph).Where(g => g.Length > 0).ToList();
        var parts = new List<string> { Labels.ScopeSurfaces(rows.Count) };
        string[] known = ["mapped", "new", "unmapped"];
        parts.AddRange(known.Concat(words.Distinct(StringComparer.Ordinal).Except(known, StringComparer.Ordinal))
            .Select(w => (Word: w, Count: words.Count(x => string.Equals(x, w, StringComparison.Ordinal))))
            .Where(p => p.Count > 0)
            .Select(p => Labels.ScopeGraphCount(p.Count, p.Word)));
        return string.Join(" · ", parts);
    }

    private static List<ScopeSection> Sectioned(JsonElement body)
    {
        var sections = new List<ScopeSection>();
        foreach (var (key, heading) in new[] { ("workstreams", Labels.ScopeWorkstreams), ("flagged", Labels.ScopeFlagged), ("excluded", Labels.ScopeExcluded), ("unresolved", Labels.ScopeUnresolved) })
        {
            if (body.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0)
            {
                sections.Add(new ScopeSection(heading, [.. list.EnumerateArray().Select(Line).Where(l => l.Length > 0)]));
            }
        }

        return sections;
    }

    private static string Line(JsonElement entry)
    {
        if (entry.ValueKind == JsonValueKind.String)
        {
            return FactText.Scrub(entry.GetString() ?? string.Empty);
        }

        if (entry.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var head = string.Join(' ', _headKeys.Select(k => Text(entry, k)).Where(s => s.Length > 0).Take(2));
        var why = string.Join(' ', _whyKeys.Select(k => Text(entry, k)).Where(s => s.Length > 0).Take(1));
        return why.Length == 0 ? head : head.Length == 0 ? why : $"{head}: {why}";
    }

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? FactText.Scrub(value.GetString() ?? string.Empty) : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : string.Empty;
    }

    private static int? Length(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : null;
}
