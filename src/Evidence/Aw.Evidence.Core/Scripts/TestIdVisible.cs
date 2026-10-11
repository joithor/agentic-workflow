using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Scripts;

/// <summary>The element with <paramref name="TestId"/> is visible.</summary>
/// <param name="TestId">The element's data-testid.</param>
public sealed record TestIdVisible(TestId TestId) : ExpectedState
{
    /// <inheritdoc/>
    public override string Describe() => $"element {TestId.Value} visible";

    /// <inheritdoc/>
    public override string? Problem() => null;
}
