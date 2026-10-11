using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop adapter of <see cref="IEndpointRecordSource"/>: control.json on the local disk.</summary>
/// <param name="path">The file.</param>
internal sealed class FileEndpointRecords(string path) : IEndpointRecordSource
{
    public string Location => path;

    public async ValueTask<byte[]?> Read(CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
