using Aw.Evidence.Core.Publishing;

namespace Aw.Evidence.Testing;

/// <summary>A runner arranged by a <see cref="GhRunnerContract"/> subclass.</summary>
/// <param name="Runner">The runner under test.</param>
public sealed record GhUnderTest(IGhRunner Runner);
