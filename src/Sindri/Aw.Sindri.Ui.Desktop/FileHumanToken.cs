using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop adapter of <see cref="IHumanTokenSource"/>: auth/human.token on the local disk, read at each call.</summary>
/// <param name="path">The file.</param>
internal sealed class FileHumanToken(string path) : IHumanTokenSource
{
    public string Location => path;

    public async ValueTask<string?> Read(CancellationToken cancellationToken)
    {
        try
        {
            var text = (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
            return text.Length == 0 ? null : text;
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
