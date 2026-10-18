namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context page (screens spec §4.4). Words shared by the page's stages live here; each stage adds its own words to its own Labels.Context*.cs partial. Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The page head before any map has been read.</summary>
    internal const string ContextNoMap = "no map";

    /// <summary>The anchor box's name.</summary>
    internal const string ContextAnchor = "Anchor";

    /// <summary>The anchor box's hint.</summary>
    internal const string ContextAnchorHint = "Type an anchor key, such as feature:name";
}
