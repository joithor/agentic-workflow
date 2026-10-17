namespace Aw.Forge.Infrastructure.World;

/// <summary>The world in memory, for the fake.</summary>
internal sealed class MemoryWorldStore : IWorldStore
{
    private readonly object _gate = new();
    private readonly WorldState _state = new();

    public TResult Read<TResult>(Func<WorldState, TResult> read)
    {
        lock (_gate)
        {
            return read(_state);
        }
    }

    public TResult Write<TResult>(Func<WorldState, TResult> write)
    {
        lock (_gate)
        {
            return write(_state);
        }
    }
}
