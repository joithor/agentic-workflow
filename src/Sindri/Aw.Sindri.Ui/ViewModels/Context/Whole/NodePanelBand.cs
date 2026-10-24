namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The band a link's other node sits in, as the node panel groups them (owner answer q95).</summary>
internal enum NodePanelBand
{
    /// <summary>Records and decisions.</summary>
    Why,

    /// <summary>Features, screens, actions, things and flows.</summary>
    Product,

    /// <summary>Code files.</summary>
    Code,

    /// <summary>Any kind this build does not know, or a node that is not on the map.</summary>
    Other,
}
