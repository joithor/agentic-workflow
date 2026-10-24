namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The three bands of the map, as F3's context page draws them: why we are doing this, what it touches in the product, what it touches in the code.</summary>
internal enum WholeBand
{
    /// <summary>Decisions and records.</summary>
    Intent,

    /// <summary>Features, surfaces, actions, things and flows.</summary>
    Product,

    /// <summary>Code files.</summary>
    Code,
}
