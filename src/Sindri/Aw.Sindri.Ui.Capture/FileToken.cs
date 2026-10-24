using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Capture;

/// <summary>The control plane's per-start human token (auth/human.token), read from disk at each call.</summary>
/// <param name="path">The file.</param>
internal sealed class FileToken(string path) : IHumanTokenSource
{
    public string Location => path;

    public async ValueTask<string?> Read(CancellationToken cancellationToken)
    {
        try
        {
            var text = (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
            return text.Length == 0 ? null : text;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}
