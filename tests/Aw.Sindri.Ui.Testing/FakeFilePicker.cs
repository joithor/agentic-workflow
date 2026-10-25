using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory file picker: answers the file a test queued, in order, and records what it was asked. Like every picker it leaves a file larger than the limit unread.</summary>
internal sealed class FakeFilePicker : IFilePicker
{
    private readonly Queue<PickedFile?> _next = new();
    private readonly List<(string Title, int Limit)> _asked = [];

    /// <summary>What the picker was asked for, in order.</summary>
    public IReadOnlyList<(string Title, int Limit)> Asked => _asked;

    /// <summary>Queues the answer to the next ask; null is "no file chosen".</summary>
    /// <param name="file">The file, or null.</param>
    public void Queue(PickedFile? file) => _next.Enqueue(file);

    public Task<PickedFile?> Pick(string title, int limit, CancellationToken cancellationToken)
    {
        _asked.Add((title, limit));
        var answer = _next.TryDequeue(out var file) ? file : null;
        return Task.FromResult(answer is { Size: var size } && size > limit ? answer with { Content = ReadOnlyMemory<byte>.Empty } : answer);
    }
}
