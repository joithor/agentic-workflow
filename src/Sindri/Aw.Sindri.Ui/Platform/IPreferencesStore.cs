namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// Port: the app's local preferences (UI spec §12.3; on a desktop $AW_SINDRI_HOME/ui/preferences.json, 0600, written to a
/// temp file and renamed). Nothing in them changes what the API decides. A store parses and writes with
/// <see cref="UiPreferences.FromJson"/> and <see cref="UiPreferences.ToJson"/>, so every head clamps and resets the same way.
/// </summary>
public interface IPreferencesStore
{
    /// <summary>Reads the preferences: clamped, and <see cref="UiPreferences.Defaults"/> when there are none or they are malformed.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The preferences.</returns>
    public ValueTask<UiPreferences> Read(CancellationToken cancellationToken);

    /// <summary>Replaces the preferences atomically (a reader sees the old file or the new one, never part of one).</summary>
    /// <param name="preferences">The preferences.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when they are written.</returns>
    public ValueTask Write(UiPreferences preferences, CancellationToken cancellationToken);
}
