using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels.Settings;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// Where Settings' appearance meets the theme: the apply callback a <see cref="SettingsViewModel"/> is built with. It
/// scales the type ramp (<see cref="TextScale"/>) and sets the motion durations (<see cref="Motion"/>) in the app's
/// resources; <see cref="Follow"/> keeps a window's reduced-motion class for the stock controls.
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

    /// <summary>The window class under which Theme/FluentOverrides.axaml turns stock controls' own transitions off.</summary>
    internal const string ReducedMotionClass = "reduce-motion";

    /// <summary>
    /// Keeps <paramref name="root"/>'s <see cref="ReducedMotionClass"/> in step with <paramref name="settings"/>: stock
    /// Fluent controls animate on fixed durations the Motion.* tokens don't reach (§11.4).
    /// </summary>
    /// <param name="root">The window.</param>
    /// <param name="settings">Settings, whose reduced motion the class follows (it raises its changes on the UI thread).</param>
    internal static void Follow(StyledElement root, SettingsViewModel settings)
    {
        root.Classes.Set(ReducedMotionClass, settings.IsMotionReduced);
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.IsMotionReduced))
            {
                root.Classes.Set(ReducedMotionClass, settings.IsMotionReduced);
            }
        };
    }
}
