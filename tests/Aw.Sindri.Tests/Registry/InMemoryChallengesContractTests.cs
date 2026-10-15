using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Testing.Challenges;

namespace Aw.Sindri.Tests.Registry;

/// <summary>The in-memory challenges keep the challenge contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryChallengesContractTests : ChallengesContract
{
    protected override IChallenges Create() => new InMemoryChallenges(Clock);
}
