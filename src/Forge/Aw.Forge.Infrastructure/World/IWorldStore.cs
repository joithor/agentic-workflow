namespace Aw.Forge.Infrastructure.World;

/// <summary>Where the world lives: in memory, or in a file. Every call sees a consistent state; a write persists before it returns.</summary>
internal interface IWorldStore
{
    TResult Read<TResult>(Func<WorldState, TResult> read);

    TResult Write<TResult>(Func<WorldState, TResult> write);
}
