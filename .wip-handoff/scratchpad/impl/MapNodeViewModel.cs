using Aw.Sindri.Ui.Views.Context.Map;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One node of the map as the view draws it: its place and its words, composed here so the view binds and draws.</summary>
internal sealed class MapNodeViewModel
{
    /// <summary>A node from its layout.</summary>
    /// <param name="layout">Where the node sits and what was served about it.</param>
    internal MapNodeViewModel(MapNodeLayout layout)
    {
        Layout = layout;
        var kindWord = Labels.ContextMapKindWord(layout.Kind);
        Name = Labels.ContextMapNodeName(kindWord, layout.Label);
        IsCard = layout.Shape == MapShape.Card;
        var sure = layout.IsSuggested ? Labels.ContextMapSuggestedPct((int)Math.Round(layout.Confidence * 100, MidpointRounding.AwayFromZero)) : null;
        Caption = IsCard
            ? Join(kindWord, layout.IsAnchor ? Labels.ContextMapAnchorDetail : layout.Detail, sure)
            : layout.Band == MapBand.Code ? Join(layout.IsTest ? kindWord.ToLowerInvariant() : null, layout.Detail, sure) : Join(kindWord.ToLowerInvariant(), layout.Detail, sure);
        (FootWord, FootRight, FootIcon) = layout switch
        {
            { IsAnchor: true } => (Labels.ContextMapInMap, Labels.ContextMapStartsHereFoot, "Ok"),
            { HasConflict: true } => (Labels.ContextMapConflictFoot, layout.Detail, "Kind.Warn"),
            _ => (string.Empty, string.Empty, string.Empty),
        };
        KindIcon = Icon(layout.Kind);
    }

    /// <summary>The layout this node was made from.</summary>
    internal MapNodeLayout Layout { get; }

    /// <summary>The node's key.</summary>
    public string Key => Layout.Key;

    /// <summary>The name for assistive technology: the kind word, a colon and the label.</summary>
    public string Name { get; }

    /// <summary>The label, up to two lines.</summary>
    public string Label => Layout.Label;

    /// <summary>The caption: the kind plus its technical detail ("route · POST /api/links").</summary>
    public string Caption { get; }

    /// <summary>The name of the kind's icon in Theme/Icons.axaml, without the "Icon." prefix.</summary>
    public string KindIcon { get; }

    /// <summary>The footer's word on a card (empty when the card has no footer).</summary>
    public string FootWord { get; }

    /// <summary>The footer's right-hand words.</summary>
    public string FootRight { get; }

    /// <summary>The footer's icon.</summary>
    public string FootIcon { get; }

    /// <summary>Whether the footer's icon is the tick (the node is in the map).</summary>
    public bool FootIsOk => string.Equals(FootIcon, "Ok", StringComparison.Ordinal);

    /// <summary>Whether the footer's icon is the warning (the decision is in a conflict).</summary>
    public bool FootIsWarn => string.Equals(FootIcon, "Kind.Warn", StringComparison.Ordinal);

    /// <summary>Whether the card has a footer.</summary>
    public bool HasFoot => FootWord.Length > 0;

    /// <summary>Left edge on the world.</summary>
    public double X => Layout.X;

    /// <summary>Top edge on the world.</summary>
    public double Y => Layout.Y;

    /// <summary>Width.</summary>
    public double Width => Layout.Width;

    /// <summary>Height.</summary>
    public double Height => Layout.Height;

    /// <summary>Whether the node is a card (else a chip).</summary>
    public bool IsCard { get; }

    /// <summary>Whether the node is a chip.</summary>
    public bool IsChip => !IsCard;

    /// <summary>Whether the read starts at this node.</summary>
    public bool IsAnchor => Layout.IsAnchor;

    /// <summary>Whether this is a suggestion nobody confirmed (dashed, with the caption words).</summary>
    public bool IsSuggested => Layout.IsSuggested;

    /// <summary>Whether this is a confirmed or found node (solid outline).</summary>
    public bool IsSolid => !Layout.IsSuggested;

    /// <summary>Whether the label is a file name (drawn in the mono face).</summary>
    public bool IsMono => Layout.Band == MapBand.Code;

    /// <summary>The "Starts here" pill's word.</summary>
    public string StartsHere { get; } = Labels.ContextMapStartsHere;

    // The kind's mark in Theme/Icons.axaml; a kind this build does not know gets the plain dot.
    private static string Icon(string kind) => kind switch
    {
        "record" or "ticket" => "Kind.Ticket",
        "decision" => "Kind.Decision",
        "feature" => "Kind.Feature",
        "screen" => "Kind.Screen",
        "route" => "Kind.Route",
        "dialog" => "Kind.Dialog",
        "email" or "api" => "Kind.Email",
        "action" => "Kind.Action",
        "data" => "Kind.Data",
        "code" or "file" => "Kind.File",
        "test" => "Kind.Test",
        _ => "Kind.Dot",
    };

    private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
}
