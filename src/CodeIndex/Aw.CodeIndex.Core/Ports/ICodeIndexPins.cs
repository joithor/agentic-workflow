namespace Aw.CodeIndex.Core.Ports;

/// <summary>Reads 'context/code-index.lock' and compares it with the pins the build carries (spec §8, the 'code-index:lock' doctor row): a file read, no tool is run.</summary>
public interface ICodeIndexPins
{
    /// <summary>Whether the lock file parses and its pins are the build's own.</summary>
    /// <returns>The state.</returns>
    public LockState Check();
}
