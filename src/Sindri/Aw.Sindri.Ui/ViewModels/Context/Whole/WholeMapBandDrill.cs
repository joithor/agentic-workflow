#pragma warning disable MA0182 // behaviour-free stub of the red commit
namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Drilling into one band of a concept's map (#1289): the pure decision of what stays drawn, and what each band that stays says it holds.</summary>
internal static class WholeMapBandDrill
{
    /// <summary>The links between a band and the other bands: the band's nodes that have one, the nodes on their other ends, and only those links.</summary>
    /// <param name="geometry">The map's geometry.</param>
    /// <param name="band">The band drilled into.</param>
    /// <returns>The geometry cut down; the bands that still hold a node count what is drawn.</returns>
    internal static WholeMapGeometry Cross(WholeMapGeometry geometry, WholeBand band) => throw new NotSupportedException();

    /// <summary>The band's own nodes and only the links with both ends in it.</summary>
    /// <param name="geometry">The map's geometry.</param>
    /// <param name="band">The band drilled into.</param>
    /// <returns>The geometry cut down.</returns>
    internal static WholeMapGeometry Own(WholeMapGeometry geometry, WholeBand band) => throw new NotSupportedException();
}
