namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// Port: the operating system's reduce-motion setting (UI spec §11.4). The desktop head adapts it per platform (macOS
/// NSWorkspace.accessibilityDisplayShouldReduceMotion, Windows SPI_GETCLIENTAREAANIMATION, GNOME enable-animations). The
/// in-app switch (Settings, <see cref="UiPreferences.ReduceMotion"/>) forces the reduced path whatever this says.
/// </summary>
public interface IMotionPreference
{
    /// <summary>True when the OS asks for reduced motion (false when the platform can't tell).</summary>
    public bool ReduceMotion { get; }

    /// <summary>Raised when <see cref="ReduceMotion"/> changes; the app re-reads it. May be raised off the UI thread.</summary>
    public event EventHandler? Changed;
}
