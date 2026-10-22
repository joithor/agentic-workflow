namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>The three bands of the context map, top to bottom.</summary>
internal enum MapBand
{
    /// <summary>Why we are doing this: tickets and decisions.</summary>
    Intent,

    /// <summary>What it touches in the product.</summary>
    Product,

    /// <summary>What it touches in the code.</summary>
    Code,
}
