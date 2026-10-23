using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// The Context page's first state (whole-map spec §3.1): the whole map, read when the page opens. Plain numbers and words; the view draws them.
/// It is loading while the read is in flight, ready with an answer (nodes in columns, or one tile per feature when the map is over the node budget),
/// or failed with Retry, keeping whatever was drawn before.
/// </summary>
internal sealed class WholeMapViewModel : ObservableObject
{
    /// <summary>Below this zoom the nodes are drawn as dots in their area's colour with one label per area (spec §3.2, about 35 %).</summary>
    internal const double DotsBelow = 0.35;

    /// <summary>The smallest scale the first open fits to: below it the whole map is too small to read even as dots, so the first open shows less than all of it.</summary>
    internal const double FirstOpenFloor = 0.25;

    /// <summary>The largest scale the map is drawn at.</summary>
    internal const double MaxScale = 1.0;

    private readonly Func<TimeZoneInfo> _zone;

    internal WholeMapViewModel(Func<TimeZoneInfo>? zone = null)
    {
        _zone = zone ?? (() => TimeZoneInfo.Local);
    }

    /// <summary>Raised when a feature asks the camera to go to its tile (a click, Enter, or opening it).</summary>
    internal event EventHandler<ValueEventArgs<WholeRect>>? ZoomRequested;

    /// <summary>Raised when a node of the map is clicked.</summary>
    internal event EventHandler<ValueEventArgs<string>>? NodeChosen;

    /// <summary>Whether the first read has ended with an answer.</summary>
    public bool IsReady { get; private set; }

    /// <summary>Whether a read is in flight.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Whether the latest read failed.</summary>
    public bool IsFailed { get; private set; }

    /// <summary>The line over the stage while reading.</summary>
    public string LoadingText { get; } = Labels.ContextWholeLoading;

    /// <summary>The failure line.</summary>
    public string FailedText { get; } = Labels.ContextWholeFailed;

    /// <summary>The Retry button's words.</summary>
    public string RetryText { get; } = Labels.ContextWholeRetry;

    /// <summary>The answer drawn; null before the first.</summary>
    internal WholeMapView? Answer { get; private set; }

    /// <summary>Where everything sits; null before the first answer.</summary>
    internal WholeMapGeometry? Geometry { get; private set; }

    /// <summary>The head: "Context · the whole map · v3"; empty before the first answer.</summary>
    public string Head { get; private set; } = string.Empty;

    /// <summary>Why the map is grouped by feature; empty on a node-level map.</summary>
    public string ByAreaNote { get; private set; } = string.Empty;

    /// <summary>Whether the note shows.</summary>
    public bool HasNote => ByAreaNote.Length > 0;

    /// <summary>The map's size and age: "412 items · 655 links · built 23 Oct 2026 06:00"; empty before the first answer.</summary>
    public string SizeText { get; private set; } = string.Empty;

    /// <summary>Whether the map is drawn by feature (tiles) rather than node by node.</summary>
    public bool ByArea => Geometry?.ByArea == true;

    /// <summary>The accessible name of the map and of the list of features.</summary>
    public string CanvasName { get; } = Labels.ContextWholeCanvasName;

    /// <summary>The list of features (J and K walk it; Enter opens one): every feature, in the order the map shows them.</summary>
    public string AreasName { get; } = Labels.ContextWholeAreasName;

    /// <summary>The features, in the order the map shows them.</summary>
    public IReadOnlyList<WholeAreaTileViewModel> Areas { get; private set; } = [];

    /// <summary>The scale the first open fits the world to: the fit, never below <see cref="FirstOpenFloor"/>.</summary>
    /// <param name="fit">The scale that shows the whole world.</param>
    /// <returns>The scale to open at.</returns>
    internal static double FirstOpenScale(double fit) => Math.Clamp(fit, FirstOpenFloor, MaxScale);

    /// <summary>Whether nodes are drawn as dots at a scale.</summary>
    /// <param name="scale">The zoom scale.</param>
    /// <returns>True below <see cref="DotsBelow"/>.</returns>
    internal static bool UsesDots(double scale) => scale < DotsBelow;

    /// <summary>A read starts: a quiet row over the stage; whatever was drawn stays.</summary>
    internal void Loading()
    {
        (IsLoading, IsFailed) = (true, false);
        Changed();
    }

    /// <summary>A read ended with something other than an answer or a failure (no map yet): nothing to show and nothing pending.</summary>
    internal void Quiet()
    {
        (IsLoading, IsFailed) = (false, false);
        Changed();
    }

    /// <summary>A read failed: the failure line with Retry, keeping what was drawn.</summary>
    internal void Failed()
    {
        (IsLoading, IsFailed) = (false, true);
        Changed();
    }

    /// <summary>A read ended: the answer is drawn.</summary>
    /// <param name="answer">The whole map.</param>
    internal void Show(WholeMapView answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        Answer = answer;
        Geometry = WholeMapLayout.Place(answer);
        Head = Labels.ContextWholeHead(answer.MapVersion);
        ByAreaNote = Geometry.ByArea ? Labels.ContextWholeByArea(answer.Counts.Nodes) : string.Empty;
        SizeText = Labels.ContextWholeSize(answer.Counts.Nodes, answer.Counts.Edges, TimeZoneInfo.ConvertTime(answer.AsOf, _zone()));
        var byKey = answer.Areas.ToDictionary(a => a.Key, StringComparer.Ordinal);
        Areas = [.. Geometry.Order.Select((key, i) => new WholeAreaTileViewModel(byKey[key], i, OpenArea))];
        (IsReady, IsLoading, IsFailed) = (true, false, false);
        Changed();
    }

    /// <summary>Opens a feature: until the route that answers one feature in full is there, the camera goes to its tile or column.</summary>
    /// <param name="key">The area's key.</param>
    internal void OpenArea(string key)
    {
        if (Geometry is { } geometry && TargetOf(geometry, key) is { } target)
        {
            ZoomRequested?.Invoke(this, new ValueEventArgs<WholeRect>(target));
        }
    }

    /// <summary>A node was clicked.</summary>
    /// <param name="key">The node's key.</param>
    internal void ChooseNode(string key) => NodeChosen?.Invoke(this, new ValueEventArgs<string>(key));

    private static WholeRect? TargetOf(WholeMapGeometry geometry, string key) =>
        geometry.Tiles.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.Ordinal))?.Rect
        ?? geometry.Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal))?.Rect;

    private void Changed()
    {
        foreach (var name in (string[])[nameof(IsReady), nameof(IsLoading), nameof(IsFailed), nameof(Head), nameof(ByAreaNote), nameof(HasNote), nameof(SizeText), nameof(ByArea), nameof(Areas)])
        {
            OnChanged(name);
        }
    }
}
