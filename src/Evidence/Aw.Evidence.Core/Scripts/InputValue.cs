using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Scripts;

/// <summary>The input with <paramref name="TestId"/> holds exactly <paramref name="Value"/>.</summary>
/// <param name="TestId">The input's data-testid.</param>
/// <param name="Value">The expected value (may be empty).</param>
public sealed record InputValue(TestId TestId, string Value) : ExpectedState
{
    /// <inheritdoc/>
    public override string Describe() => $"input {TestId.Value} has value \"{Value}\"";

    /// <inheritdoc/>
    public override string? Problem() => null;
}
