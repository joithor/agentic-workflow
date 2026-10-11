namespace Aw.Evidence.Core.Locks;

/// <summary>A held lock; disposing releases it (more than once is harmless).</summary>
public interface ILockLease : IAsyncDisposable
{
    /// <summary>The locks this lease holds, widest first.</summary>
    public IReadOnlyList<LockKind> Held { get; }
}
