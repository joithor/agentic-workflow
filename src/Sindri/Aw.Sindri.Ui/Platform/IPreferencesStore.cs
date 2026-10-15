namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// Port: where the app keeps its local preferences (UI spec §12.3; on a desktop $AW_SINDRI_HOME/ui/preferences.json,
/// 0600). Like the endpoint record, a head only moves bytes: the app parses, clamps and resets them (an unreadable file
/// reads as the defaults), so every head behaves the same. Nothing in them changes what the API decides.
/// </summary>
public interface IPreferencesStore
{
    /// <summary>Where the preferences are kept, for messages (a path on a desktop).</summary>
    public string Location { get; }

    /// <summary>Reads the stored bytes.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or null when nothing is stored.</returns>
    public ValueTask<byte[]?> Read(CancellationToken cancellationToken);

    /// <summary>Replaces the stored bytes atomically (a reader sees the old contents or the new, never part of one), readable by the user only.</summary>
    /// <param name="contents">The new contents.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when they are written.</returns>
    public ValueTask Write(byte[] contents, CancellationToken cancellationToken);
}
