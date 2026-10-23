namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The fill that tells an area apart beyond its colour: a feature is never told by colour alone, and a palette colour reused by a later area gets the next pattern.</summary>
internal enum WholePattern
{
    /// <summary>A plain fill.</summary>
    Solid,

    /// <summary>Diagonal lines.</summary>
    Hatched,

    /// <summary>A ring around the dot.</summary>
    Ringed,
}
