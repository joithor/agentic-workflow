using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>
/// Settings (UI spec §3.3 ⌘,, §11.4, §12.3, §14.2): the in-app Reduce motion switch and the text size, kept in the local
/// preferences. Reduced motion is the OS setting or the switch, re-read when the OS setting changes. Each change goes to
/// the theme through the apply callback (no Avalonia types here).
/// </summary>
[SuppressMessage("Design", "MA0182:Internal type is apparently never used", Justification = "U0 Unit D: the integrator builds it in SindriApp.Create(SindriPlatform, TimeProvider) for the shell's Settings page.")]
internal sealed class SettingsViewModel : ObservableObject, IDisposable
{
    /// <summary>Settings over the preferences, the OS setting and the theme.</summary>
    /// <param name="preferences">The local preferences.</param>
    /// <param name="motion">The OS reduce-motion setting.</param>
    /// <param name="apply">Applies the appearance to the theme.</param>
    internal SettingsViewModel(Preferences preferences, IMotionPreference motion, Action<AppearanceSettings> apply)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(apply);
        throw new NotSupportedException();
    }

    /// <summary>The in-app Reduce motion switch.</summary>
    public bool ReduceMotion { get; set; }

    /// <summary>True while the OS asks for reduced motion.</summary>
    public bool SystemReducesMotion { get; }

    /// <summary>True when motion is reduced: the OS setting or the switch.</summary>
    public bool IsMotionReduced { get; }

    /// <summary>The text size (0.9–1.3).</summary>
    public double TextScale { get; set; }

    /// <summary>The text size in words ("110%").</summary>
    public string TextScaleText { get; } = string.Empty;

    /// <summary>The latest preferences write; complete when nothing is being written.</summary>
    internal Task Saving { get; } = Task.CompletedTask;

    /// <summary>Reads the stored preferences and the OS setting, and applies them (the shell's start-up calls it once).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when they're applied.</returns>
    internal Task Load(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public void Dispose() => throw new NotSupportedException();
}
