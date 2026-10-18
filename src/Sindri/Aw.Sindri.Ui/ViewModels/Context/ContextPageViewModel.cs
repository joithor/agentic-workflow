using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The Context page (screens spec §4.4): a head, the lens stack and the side column. U9a0 only draws the skeleton, opened empty
/// with no read made; U9a owns this file from here on, and the seams (<see cref="Review"/>, <see cref="FileMap"/>,
/// <see cref="Node"/>, <see cref="Sources"/>) live in the partial files named for the stage that owns each.
/// </summary>
internal sealed partial class ContextPageViewModel : ObservableObject
{
    /// <summary>The page over the API.</summary>
    /// <param name="api">Sindri's API.</param>
    internal ContextPageViewModel(ISindriApi api)
    {
        Api = api;
    }

    /// <summary>Sindri's API, for the stages that read through it.</summary>
    internal ISindriApi Api { get; }

    /// <summary>The page's title.</summary>
    public string Title { get; } = Labels.Context;

    /// <summary>The head line: what map the page shows. Empty until something has been read; "no map" is for the served no-map case (U9a).</summary>
    public string Head { get; } = string.Empty;

    /// <summary>Whether the head has words to show.</summary>
    public bool HasHead => Head.Length > 0;

    /// <summary>The anchor box's name.</summary>
    public string AnchorLabel { get; } = Labels.ContextAnchor;

    /// <summary>The anchor box's hint.</summary>
    public string AnchorHint { get; } = Labels.ContextAnchorHint;

    /// <summary>What is typed in the anchor box.</summary>
    public string AnchorText
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;
}
