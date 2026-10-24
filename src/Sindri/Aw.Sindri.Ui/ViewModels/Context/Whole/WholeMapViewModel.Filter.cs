namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole map's search filter (<see cref="WholeMapViewModel"/>, continued): stub, not yet filtering.</summary>
internal sealed partial class WholeMapViewModel
{
    /// <summary>What is drawn and hit.</summary>
    internal WholeMapGeometry? Shown => Geometry;

    /// <summary>The box the camera fits.</summary>
    internal WholeRect? FitBox => Geometry?.World;
}
