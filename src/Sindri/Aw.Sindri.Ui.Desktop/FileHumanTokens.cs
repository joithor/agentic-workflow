using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop adapter of <see cref="IHumanTokenSource"/>: '&lt;home&gt;/auth/human.token', which each 'sindri serve' mints (0600).</summary>
/// <param name="path">The file.</param>
internal sealed class FileHumanTokens(string path) : IHumanTokenSource
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
