using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole map's zoom onto a query's answer (<see cref="WholeMapViewModel"/>, continued; whole-map spec §3.1 "Zoomed in").</summary>
internal sealed partial class WholeMapViewModel
{
    /// <summary>The nodes a query's answer holds. While zoomed, the map draws these and nothing else; empty when nothing is zoomed to.</summary>
    internal IReadOnlySet<string> HitKeys { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Whether the map read is the overview of features, which a query reaches through <c>locate</c>. It stays so while zoomed, when the drawn geometry holds nodes.</summary>
    internal bool AnswerByArea => Answer is { } answer && string.Equals(answer.Detail, "areas", StringComparison.Ordinal);

    /// <summary>Whether the camera is on a query's answer.</summary>
    public bool IsZoomed { get; private set; }

    /// <summary>The "Whole map" button's words.</summary>
    public string BackText { get; } = Labels.ContextWholeBack;

    /// <summary>The card's title.</summary>
    public string CardTitle { get; } = Labels.ContextMapWhyTitle;

    /// <summary>The card's words: pick a starting point until one is chosen, then what the map says about it.</summary>
    public string CardText => IsZoomed ? Labels.ContextMapWhyNone : Labels.ContextWholePick;

    /// <summary>"2 more features touched" once a query touched more features than were opened; empty otherwise.</summary>
    public string MoreTouchedText { get; private set; } = string.Empty;

    /// <summary>Whether the more-touched chip shows.</summary>
    public bool HasMoreTouched => MoreTouchedText.Length > 0;

    private (WholeMapView? Answer, WholeMapGeometry? Geometry, IReadOnlyList<WholeAreaTileViewModel> Areas)? _beforeZoom;

    /// <summary>
    /// The camera goes to a query's answer. Only that answer's nodes, the links between them, and the features that hold them stay;
    /// the camera eases to the box around the nodes. "Whole map" puts the rest back.
    /// </summary>
    /// <param name="keys">The keys of the answer's nodes.</param>
    /// <returns>True when any of them is on the map; false moves nothing.</returns>
    internal bool Zoom(IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (Answer is not { Nodes: { Count: > 0 } nodes } answer || Geometry is null)
        {
            return false;
        }

        var hitKeys = keys.ToHashSet(StringComparer.Ordinal);
        var hit = nodes.Where(n => hitKeys.Contains(n.Key)).ToList();
        if (hit.Count == 0)
        {
            return false;
        }

        var areaKeys = hit.Select(n => n.Area).ToHashSet(StringComparer.Ordinal);
        var filtered = answer with
        {
            Nodes = hit,
            Edges = [.. (answer.Edges ?? []).Where(e => hitKeys.Contains(e.From) && hitKeys.Contains(e.To))],
            Areas = [.. answer.Areas.Where(a => areaKeys.Contains(a.Key))],
            AreaEdges = [.. answer.AreaEdges.Where(e => areaKeys.Contains(e.From) && areaKeys.Contains(e.To))],
        };
        var geometry = WholeMapLayout.Place(filtered);
        return geometry.Nodes.Count > 0 && ApplyZoom(geometry, filtered.Areas, string.Empty);
    }

    /// <summary>
    /// The camera goes to a query's answer by feature. The opened features keep the answer's nodes only, and features the answer does not touch are left off the graph and off the chip list.
    /// </summary>
    /// <param name="overview">The overview the answer sits in.</param>
    /// <param name="opened">The opened features' answers.</param>
    /// <param name="keys">The keys of the answer's nodes.</param>
    /// <param name="notOpened">How many touched features were not opened. Their names are not here, so they are counted and not drawn.</param>
    /// <returns>True when any answer node is in an opened feature; false changes nothing.</returns>
    internal bool ZoomIntoAreas(WholeMapView overview, IReadOnlyList<WholeMapView> opened, IReadOnlyCollection<string> keys, int notOpened)
    {
        ArgumentNullException.ThrowIfNull(opened);
        var touched = new List<string>();
        foreach (var area in opened)
        {
            if (area.Area is { } info)
            {
                touched.Add(info.Key);
            }
        }

        return ZoomIntoAreas(overview, opened, keys, touched, notOpened);
    }

    /// <summary>
    /// The camera goes to a query's answer by feature. Chips and tiles are the features the answer touches; opened ones show the answer's nodes.
    /// </summary>
    /// <param name="overview">The overview the answer sits in.</param>
    /// <param name="opened">The opened features' answers.</param>
    /// <param name="keys">The keys of the answer's nodes.</param>
    /// <param name="touchedAreas">Every feature the answer touches, opened or only counted.</param>
    /// <returns>True when any answer node is in an opened feature; false changes nothing.</returns>
    internal bool ZoomIntoAreas(WholeMapView overview, IReadOnlyList<WholeMapView> opened, IReadOnlyCollection<string> keys, IReadOnlyCollection<string> touchedAreas)
    {
        ArgumentNullException.ThrowIfNull(touchedAreas);
        var openedCount = opened.Count(area => area.Area is not null);
        return ZoomIntoAreas(overview, opened, keys, touchedAreas, Math.Max(0, touchedAreas.Count - openedCount));
    }

    /// <summary>Back to the whole map: the answer's nodes stop standing out, features opened for it close, and the camera fits the world again.</summary>
    internal void Unzoom()
    {
        if (!IsZoomed)
        {
            return;
        }

        if (_beforeZoom is { } before)
        {
            (Answer, Geometry, Areas) = before;
            _beforeZoom = null;
        }

        (HitKeys, IsZoomed, MoreTouchedText) = (new HashSet<string>(StringComparer.Ordinal), false, string.Empty);
        Changed();
        FitRequested?.Invoke(this, EventArgs.Empty);
    }

    // Features the caller named, plus any opened feature that actually holds an answer node. Nodes outside the answer are dropped before placement.
    private bool ZoomIntoAreas(WholeMapView overview, IReadOnlyList<WholeMapView> opened, IReadOnlyCollection<string> keys, IReadOnlyCollection<string> touchedAreas, int notOpened)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(keys);
        var hitKeys = keys.ToHashSet(StringComparer.Ordinal);
        var openedHits = WithHits(opened, hitKeys);
        if (openedHits.Count == 0)
        {
            return false;
        }

        var relevant = Relevant(overview, touchedAreas, openedHits);
        var geometry = WholeMapLayout.PlaceMixed(relevant, openedHits);
        var more = notOpened > 0 ? Labels.ContextWholeMoreTouched(notOpened) : string.Empty;
        return geometry.Nodes.Count > 0 && ApplyZoom(geometry, relevant.Areas, more);
    }

    private static List<WholeMapView> WithHits(IReadOnlyList<WholeMapView> opened, HashSet<string> hitKeys) =>
        [.. opened.Select(area => area with
        {
            Nodes = [.. (area.Nodes ?? []).Where(n => hitKeys.Contains(n.Key))],
            Edges = [.. (area.Edges ?? []).Where(e => hitKeys.Contains(e.From) && hitKeys.Contains(e.To))],
        }).Where(area => area.Area is not null && area.Nodes is { Count: > 0 })];

    private static WholeMapView Relevant(WholeMapView overview, IReadOnlyCollection<string> touchedAreas, List<WholeMapView> openedHits)
    {
        var touched = touchedAreas.ToHashSet(StringComparer.Ordinal);
        foreach (var area in openedHits)
        {
            if (area.Area is { } info)
            {
                touched.Add(info.Key);
            }
        }

        return overview with
        {
            Areas = [.. overview.Areas.Where(a => touched.Contains(a.Key))],
            AreaEdges = [.. overview.AreaEdges.Where(e => touched.Contains(e.From) && touched.Contains(e.To))],
        };
    }

    private bool ApplyZoom(WholeMapGeometry geometry, IReadOnlyList<WholeArea> areas, string moreTouched)
    {
        var byKey = areas.ToDictionary(a => a.Key, StringComparer.Ordinal);
        _beforeZoom ??= (Answer, Geometry, Areas);
        Geometry = geometry;
        Areas = [.. geometry.Order.Select((key, i) => new WholeAreaTileViewModel(byKey[key], i, OpenArea))];
        (HitKeys, IsZoomed, MoreTouchedText) = (geometry.Nodes.Select(n => n.Key).ToHashSet(StringComparer.Ordinal), true, moreTouched);
        Changed();
        ZoomRequested?.Invoke(this, new ValueEventArgs<WholeRect>(WholeZoomBox.Around([.. geometry.Nodes.Select(n => n.Rect)])));
        return true;
    }
}
