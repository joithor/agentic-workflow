namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// Port: where the running control plane recorded its endpoint (control.json under AW_SINDRI_HOME on a desktop). A head
/// adapts it to its platform; the app parses and checks the record itself.
/// </summary>
public interface IEndpointRecordSource
{
    /// <summary>Where the record is read from, for messages (a path on a desktop).</summary>
    public string Location { get; }

    /// <summary>Reads the record's bytes.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record's JSON bytes, or null when there is none.</returns>
    public ValueTask<byte[]?> Read(CancellationToken cancellationToken);
}
