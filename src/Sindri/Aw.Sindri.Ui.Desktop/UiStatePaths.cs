namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The app's own files on this machine (UI spec §12.3): a 'ui' directory in Sindri's home, the directory that holds
/// control.json (<see cref="ControlFilePath"/>). Owner-only: the directory is 0700 and every file in it 0600.
/// </summary>
/// <param name="Directory">The 'ui' directory.</param>
/// <param name="Preferences">preferences.json, the local preferences.</param>
/// <param name="InstanceSocket">The single instance's Unix domain socket.</param>
/// <param name="InstanceLock">The file whose lock is the single instance's claim.</param>
internal sealed record UiStatePaths(string Directory, string Preferences, string InstanceSocket, string InstanceLock)
{
    /// <summary>The paths under the home <see cref="ControlFilePath"/> resolves.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="userHome">The user's home directory.</param>
    /// <returns>The paths.</returns>
    internal static UiStatePaths From(Func<string, string?> environment, string userHome) =>
        In(Path.Combine(Path.GetDirectoryName(ControlFilePath.From(environment, userHome))!, "ui")); // why: ControlFilePath always joins a directory and a file

    /// <summary>The paths in <paramref name="directory"/>.</summary>
    /// <param name="directory">The 'ui' directory.</param>
    /// <returns>The paths.</returns>
    internal static UiStatePaths In(string directory) => new(
        directory,
        Path.Combine(directory, "preferences.json"),
        Path.Combine(directory, "instance.sock"),
        Path.Combine(directory, "instance.lock"));
}
