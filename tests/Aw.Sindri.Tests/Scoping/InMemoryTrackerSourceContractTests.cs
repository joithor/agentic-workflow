using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The in-memory tracker source honours the tracker contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryTrackerSourceContractTests : TrackerSourceContract
{
    protected override ITrackerSource Create(TrackerSeed seed) => new InMemoryTrackerSource(seed);

    protected override ITrackerSource CreateUnconfigured() => InMemoryTrackerSource.Unconfigured;
}
