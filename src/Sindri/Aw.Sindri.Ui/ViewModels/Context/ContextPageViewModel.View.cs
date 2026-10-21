namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Which view the page shows: the map, or the files (F3 spec 1 §2, "1c").</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The label of the starting points box.</summary>
    public string StartsAtLabel { get; } = Labels.ContextMapStartsAt;

    /// <summary>The label of the pinned or live choice.</summary>
    public string AsOfLabel { get; } = Labels.ContextMapAsOf;

    /// <summary>The pinned choice's words (off until the item's approved map is served).</summary>
    public string AsOfPinned { get; } = Labels.ContextMapAsOfPinned;

    /// <summary>Why the pinned choice is off.</summary>
    public string AsOfPinnedHint { get; } = Labels.ContextMapPinnedNotServed;

    /// <summary>The live choice's words.</summary>
    public string AsOfLive { get; } = Labels.ContextMapAsOfLive;

    /// <summary>The view switch's name.</summary>
    public string ViewName { get; } = Labels.ContextMapViewName;

    /// <summary>The map view's words.</summary>
    public string ViewMapWord { get; } = Labels.ContextMapViewMap;

    /// <summary>The files view's words.</summary>
    public string ViewFilesWord { get; } = Labels.ContextMapViewFiles;

    /// <summary>Whether the files view is shown (else the map).</summary>
    public bool ShowFiles
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnChanged(nameof(ShowFiles));
            OnChanged(nameof(ShowMap));
            OnChanged(nameof(MapVisible));
        }
    }

    /// <summary>Whether the map view is shown.</summary>
    public bool ShowMap => !ShowFiles;

    /// <summary>Whether the canvas is on the stage: there is a map and the map view is the one chosen.</summary>
    public bool MapVisible => HasStack && ShowMap;

    /// <summary>Chooses the view.</summary>
    /// <param name="files">True for the files view, false for the map.</param>
    internal void ChooseView(bool files) => ShowFiles = files;
}
