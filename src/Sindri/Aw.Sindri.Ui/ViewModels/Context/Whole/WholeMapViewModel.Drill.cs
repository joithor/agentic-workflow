#pragma warning disable CA1822, MA0041, IDE0060 // behaviour-free stubs of the red commit
namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Drilling into a band of a concept's map (<see cref="WholeMapViewModel"/>, continued, #1289).</summary>
internal sealed partial class WholeMapViewModel
{
    /// <summary>Gets the buttons over the map, one per drawn band, top to bottom.</summary>
    public IReadOnlyList<BandButton> BandButtons => [];

    /// <summary>Gets a value indicating whether the band buttons show: inside a concept, not drilled.</summary>
    public bool ShowBandButtons => false;

    /// <summary>Gets a value indicating whether a band is drilled into.</summary>
    public bool IsDrilled => false;

    /// <summary>Gets the words of the cross segment.</summary>
    public string CrossText => string.Empty;

    /// <summary>Gets the words of the own segment.</summary>
    public string OwnText => string.Empty;

    /// <summary>Gets or sets a value indicating whether links to the other bands are drawn.</summary>
    public bool ShowCross { get; set; }

    /// <summary>Gets or sets a value indicating whether the band's own links are drawn.</summary>
    public bool ShowOwn { get; set; }

    /// <summary>Gets the line that says the drilled view has no links; empty when it has some.</summary>
    public string DrillEmptyText => string.Empty;

    /// <summary>Gets a value indicating whether <see cref="DrillEmptyText"/> shows.</summary>
    public bool ShowDrillEmpty => false;

    /// <summary>Drills into a band: links to the other bands first.</summary>
    /// <param name="band">The band.</param>
    internal void DrillInto(WholeBand band)
    {
    }

    /// <summary>Leaves the drill: the concept's whole map again.</summary>
    /// <returns>True when a band was drilled into.</returns>
    internal bool ExitDrill() => false;

    /// <summary>A click at a point of the world: opens the band whose title is there.</summary>
    /// <param name="x">World x.</param>
    /// <param name="y">World y.</param>
    /// <returns>True when a lane title was hit.</returns>
    internal bool LaneTitleClicked(double x, double y) => false;
}
