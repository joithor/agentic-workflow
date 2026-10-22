namespace Aw.CodeIndex.Core.Ports;

/// <summary>What <see cref="ICodeIndexPins"/> found.</summary>
public enum LockState
{
    /// <summary>No lock file was found (set AW_CODE_INDEX_LOCK, or run from the toolkit's checkout).</summary>
    Missing,

    /// <summary>The file does not parse: a row is malformed or a required row is absent.</summary>
    Unparsable,

    /// <summary>It parses, but a pin differs from the one the build carries.</summary>
    Mismatch,

    /// <summary>It parses and the pins match.</summary>
    Matches,
}
