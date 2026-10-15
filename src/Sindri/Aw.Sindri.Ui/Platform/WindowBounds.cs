namespace Aw.Sindri.Ui.Platform;

/// <summary>The main window's position and size, in device-independent pixels.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width (at least <see cref="UiPreferences.MinWidth"/>).</param>
/// <param name="Height">Height (at least <see cref="UiPreferences.MinHeight"/>).</param>
internal sealed record WindowBounds(double X, double Y, double Width, double Height);
