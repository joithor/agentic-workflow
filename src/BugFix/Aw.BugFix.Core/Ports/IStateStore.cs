using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Ports;

/// <summary>Keeps a bug fix's state in its state directory.</summary>
public interface IStateStore
{
    /// <summary>The state file's full path in <paramref name="directory"/>.</summary>
    /// <param name="directory">The state directory.</param>
    /// <returns>The path.</returns>
    public string StateFile(string directory);

    /// <summary>Takes the state directory for this call; another call that holds it makes this one wait, then give up with AW-BUGFIX-021.</summary>
    /// <param name="directory">The state directory.</param>
    /// <param name="create">Whether to create a missing directory (init); otherwise a missing one is AW-BUGFIX-002.</param>
    /// <returns>The held lock (dispose to release), AW-BUGFIX-002, AW-BUGFIX-021, or AW-BUGFIX-022.</returns>
    public Result<IDisposable> Lock(string directory, bool create);

    /// <summary>Loads the state.</summary>
    /// <param name="directory">The state directory.</param>
    /// <returns>The state, AW-BUGFIX-002 when there is none, or AW-BUGFIX-003 when it can't be read.</returns>
    public Result<BugFixState> Load(string directory);

    /// <summary>Saves the state atomically: a crash never leaves a half-written file.</summary>
    /// <param name="directory">The state directory (created when missing).</param>
    /// <param name="state">The state.</param>
    public void Save(string directory, BugFixState state);
}
