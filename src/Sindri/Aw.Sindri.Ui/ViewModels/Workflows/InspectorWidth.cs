namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The run drawer's width (F3 workflows spec §2): 48 % of the window, no narrower than Size.InspectorMin and no wider than Size.Inspector.</summary>
internal static class InspectorWidth
{
    /// <summary>The narrowest the drawer goes.</summary>
    internal const double Min = 360;

    /// <summary>The widest the drawer goes.</summary>
    internal const double Max = 480;

    /// <summary>The drawer's width for a window.</summary>
    /// <param name="windowWidth">The window's width.</param>
    /// <returns>The width, from <see cref="Min"/> to <see cref="Max"/>.</returns>
    internal static double For(double windowWidth) => Math.Clamp(windowWidth * 0.48, Min, Max);
}
