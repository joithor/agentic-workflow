using Aw.CodeIndex.Core.Ports;

namespace Aw.CodeIndex.Testing;

/// <summary>A lock check with a scripted answer.</summary>
public sealed class FakePins : ICodeIndexPins
{
    /// <summary>What <see cref="Check"/> answers.</summary>
    public LockState State { get; set; } = LockState.Matches;

    /// <inheritdoc />
    public LockState Check() => State;
}
