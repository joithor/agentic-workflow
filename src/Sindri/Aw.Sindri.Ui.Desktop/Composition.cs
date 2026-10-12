using Avalonia;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The head's composition, by hand: one adapter per Aw.Sindri.Ui port, bundled as a <see cref="SindriPlatform"/> and handed
/// to the app. There is no container: a service locator would sit outside a Host (ARCH-DI-001).
/// </summary>
internal static class Composition
{
    /// <summary>How often the OS reduce-motion setting is re-read.</summary>
    internal static readonly TimeSpan MotionPoll = TimeSpan.FromSeconds(2);

    /// <summary>The desktop adapters, over the files <see cref="ControlFilePath"/> and <see cref="UiStatePaths"/> name.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="userHome">The user's home directory.</param>
    /// <param name="clock">The clock for every timing rule.</param>
    /// <returns>The adapters.</returns>
    internal static SindriPlatform Platform(Func<string, string?> environment, string userHome, TimeProvider clock)
    {
        var ui = UiStatePaths.From(environment, userHome);
        return new SindriPlatform(
            new FileEndpointRecords(ControlFilePath.From(environment, userHome)),
            new FileHumanTokens(ControlFilePath.Token(environment, userHome)),
            new FilePreferencesStore(ui.Preferences),
            new PolledMotionPreference(MotionReader(), clock, MotionPoll),
            new SocketSingleInstance(ui, clock),
            new AvaloniaTray());
    }

    /// <summary>The app over <paramref name="platform"/>.</summary>
    /// <param name="platform">The head's adapters.</param>
    /// <param name="clock">The clock for every timing rule.</param>
    /// <returns>The app's builder.</returns>
    internal static AppBuilder Compose(SindriPlatform platform, TimeProvider clock) =>
        AppBuilder.Configure(() => SindriApp.Create(platform, clock))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>This OS's reduce-motion reader: macOS NSWorkspace, Windows SPI_GETCLIENTAREAANIMATION, GNOME enable-animations; elsewhere never.</summary>
    /// <returns>The reader.</returns>
    internal static Func<bool> MotionReader()
    {
        if (OperatingSystem.IsMacOS())
        {
            return MacMotion.Read;
        }

        if (OperatingSystem.IsWindows())
        {
            return WindowsMotion.Read;
        }

        return OperatingSystem.IsLinux() ? static () => GnomeMotion.Read(GnomeMotion.Command) : static () => false;
    }
}
