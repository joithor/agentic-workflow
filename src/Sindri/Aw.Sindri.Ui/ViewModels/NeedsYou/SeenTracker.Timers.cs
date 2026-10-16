namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The tracker's timer callbacks: each runs a turn after its timer fired, and only the current timer may act.</summary>
internal sealed partial class SeenTracker
{
    // Called from the UI context a turn after the timer fired: only the timer still current for this offer may end it.
    private void ExpireUndo(long gen)
    {
        lock (_gate)
        {
            if (gen != _undoGen)
            {
                return;
            }

            _undo = [];
            StopUndoTimer();
        }

        OnChanged(nameof(UndoCount));
    }

    private void DwellElapsed(string id, long gen)
    {
        lock (_gate)
        {
            if (!_watching.ContainsKey(id) || !_dwellGen.TryGetValue(id, out var current) || current != gen)
            {
                return;
            }
        }

        MarkSeen(id);
    }

    private void StartUndoTimer()
    {
        var gen = ++_undoGen;
        _undoTimer = _clock.CreateTimer(_ => _ui.Post(() => ExpireUndo(gen)), state: null, UndoFor, Timeout.InfiniteTimeSpan);
    }

    private void StopUndoTimer()
    {
        _undoGen++;
        _undoTimer?.Dispose();
        _undoTimer = null;
    }
}
