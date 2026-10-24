using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Capture;

/// <summary>The endpoint record the running control plane wrote (control.json), read from disk at each call.</summary>
/// <param name="path">The file.</param>
internal sealed class FileRecords(string path) : IEndpointRecordSource
{
    public string Location => path;

    public async ValueTask<EndpointRecordRead> Read(CancellationToken cancellationToken)
    {
        try
        {
            return EndpointRecordRead.Found(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return EndpointRecordRead.None;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return EndpointRecordRead.Unreadable(EndpointRecordFailure.Unreadable);
        }
    }
}
