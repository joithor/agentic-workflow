using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory launcher: what was opened, or a refusal when the test says so.</summary>
internal sealed class FakeLauncher : ILauncher
{
    private readonly List<string> _opened = [];

    /// <summary>Everything opened, in order: the link's text or the file's path.</summary>
    public IReadOnlyList<string> Opened => _opened;

    /// <summary>When true, every request is refused.</summary>
    public bool Refuse { get; set; }

    public Task<bool> OpenUri(Uri uri, CancellationToken cancellationToken) => Record(uri.AbsoluteUri);

    public Task<bool> OpenFile(string path, CancellationToken cancellationToken) => Record(path);

    private Task<bool> Record(string what)
    {
        if (Refuse)
        {
            return Task.FromResult(false);
        }

        _opened.Add(what);
        return Task.FromResult(true);
    }
}
