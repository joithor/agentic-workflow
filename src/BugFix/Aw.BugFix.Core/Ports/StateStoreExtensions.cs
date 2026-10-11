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
    /// <returns>The step's result, or the lock's error (AW-BUGFIX-021, AW-BUGFIX-022).</returns>
    public static Result<T> Locked<T>(this IStateStore store, string directory, Func<Result<T>> work)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(work);
        var held = store.Lock(directory);
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
