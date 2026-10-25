using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory folder picker: answers the folder a test queued, in order, and records what it was asked. It can stand for a head with no chooser.</summary>
internal sealed class FakeFolderPicker : IFolderPicker
{
    private readonly Queue<string?> _next = new();
    private readonly List<string> _asked = [];

    /// <summary>False for a head that can't show a chooser (a browser head).</summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>The titles the picker was asked with, in order.</summary>
    public IReadOnlyList<string> Asked => _asked;

    /// <summary>Queues the answer to the next ask; null is "no folder chosen".</summary>
    /// <param name="folder">The folder's path, or null.</param>
    public void Queue(string? folder) => _next.Enqueue(folder);

    public Task<string?> Pick(string title, CancellationToken cancellationToken)
    {
        _asked.Add(title);
        return Task.FromResult(IsAvailable && _next.TryDequeue(out var folder) ? folder : null);
    }
}
