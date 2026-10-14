using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The desktop adapter of <see cref="IEndpointRecordSource"/>: control.json on the local disk. A record the platform
/// can't read fails with the port's reason: on macOS and Linux .NET throws UnauthorizedAccessException for a directory in
/// its place and for a file without read permission alike, so a directory at the path is told apart first.
/// </summary>
/// <param name="path">The file.</param>
internal sealed class FileEndpointRecords(string path) : IEndpointRecordSource
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
            var why = Directory.Exists(path) ? EndpointRecordFailure.NotAFile
                : e is UnauthorizedAccessException ? EndpointRecordFailure.NoPermission
                : EndpointRecordFailure.Unreadable;
            throw new EndpointRecordUnreadableException(why, e);
        }
    }
}
