using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Infrastructure.Challenges;
using Aw.Sindri.Testing.Challenges;

namespace Aw.Sindri.Tests.Registry;

/// <summary>The in-process challenges keep the challenge contract (engine spec §4.2).</summary>
[Lane(TestLane.Unit)]
public sealed class ProcessChallengesContractTests : ChallengesContract
{
    protected override IChallenges Create() => new ProcessChallenges(Clock, new SequentialIdGenerator());
}
