namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>What Settings asks the theme to show: the type ramp's scale and whether motion is reduced (effective: the OS setting or the in-app switch).</summary>
/// <param name="TextScale">The type ramp's scale (0.9–1.3).</param>
/// <param name="ReduceMotion">True when every motion duration is 0.</param>
internal sealed record AppearanceSettings(double TextScale, bool ReduceMotion);
