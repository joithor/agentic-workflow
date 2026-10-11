using Avalonia.Controls;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels.Settings;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// Where Settings' appearance meets the theme: the apply callback a <see cref="SettingsViewModel"/> is built with. It
/// scales the type ramp (<see cref="TextScale"/>) and sets the motion durations (<see cref="Motion"/>) in the app's
/// resources.
/// </summary>
internal static class SettingsTheme
{
    /// <summary>The callback that applies an appearance to <paramref name="resources"/> (the app's resources).</summary>
    /// <param name="resources">The app's resources, with the tokens merged in.</param>
    /// <returns>The callback.</returns>
    internal static Action<AppearanceSettings> For(IResourceDictionary resources) => appearance =>
    {
        TextScale.Apply(resources, appearance.TextScale);
        Motion.Apply(resources, appearance.ReduceMotion);
    };
}
