using Aw.Kernel;

namespace Aw.BugFix.Core.Ports;

/// <summary>Runs a state change under the state directory's lock.</summary>
public static class StateStoreExtensions
{
    /// <summary>Holds the state directory's lock while <paramref name="work"/> loads, decides and saves.</summary>
    /// <typeparam name="T">The step's result.</typeparam>
    /// <param name="store">The state store.</param>
    /// <param name="directory">The state directory.</param>
    /// <param name="work">The load-decide-save step.</param>
    /// <param name="create">Whether the step may create the state directory (init only).</param>
    /// <returns>The step's result, or the lock's error (AW-BUGFIX-002, AW-BUGFIX-021, AW-BUGFIX-022).</returns>
    public static Result<T> Locked<T>(this IStateStore store, string directory, Func<Result<T>> work, bool create = false)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(work);
        var held = store.Lock(directory, create);
        if (held.IsError)
        {
            return held.Error;
        }

        using (held.Value)
        {
            return work();
        }
    }
}
