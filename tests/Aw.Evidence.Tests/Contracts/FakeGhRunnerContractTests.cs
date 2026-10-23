using Aw.Evidence.Core.Publishing;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeGhRunnerContractTests : GhRunnerContract
{
    protected override Task<GhUnderTest> ArrangeAsync(GhProgram program) =>
        Task.FromResult(new GhUnderTest(new FakeGhRunner().Reply(_ => new GhOutcome(program.ExitCode, program.Stdout, program.Stderr))));

    protected override Task<GhUnderTest> ArrangeMissingAsync() => Task.FromResult(new GhUnderTest(new FakeGhRunner { WontStart = true }));
}
