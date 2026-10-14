namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Tests that assert on wall clock (the Phase 5 signal's "in under a second"): they run alone, after the parallel
/// tests, so the other test servers and workers on the box don't eat their second.
/// </summary>
[CollectionDefinition(nameof(WallClockAlone), DisableParallelization = true)]
public sealed class WallClockAlone;
