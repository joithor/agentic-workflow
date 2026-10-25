using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>A move on the map's world, in px.</summary>
/// <param name="X">Along the left to right axis.</param>
/// <param name="Y">Along the top to bottom axis.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct MapVector(double X, double Y)
{
    /// <summary>Scales a vector.</summary>
    /// <param name="vector">The vector.</param>
    /// <param name="factor">The factor.</param>
    /// <returns>The scaled vector.</returns>
    public static MapVector operator *(MapVector vector, double factor) => Multiply(vector, factor);

    /// <summary>Scales a vector.</summary>
    /// <param name="vector">The vector.</param>
    /// <param name="factor">The factor.</param>
    /// <returns>The scaled vector.</returns>
    public static MapVector Multiply(MapVector vector, double factor) => new(vector.X * factor, vector.Y * factor);
}
