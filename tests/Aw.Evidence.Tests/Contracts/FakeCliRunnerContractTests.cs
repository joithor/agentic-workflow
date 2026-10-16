using Aw.Evidence.Core.Data;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeCliRunnerContractTests : CliRunnerContract
{
    private const string Tool = "/work/repo/out/tool";

    protected override Task<CliUnderTest> ArrangeAsync(CliProgram program) =>
        Task.FromResult(new CliUnderTest(new FakeCliRunner().Program(Tool, new CliOutcome(program.ExitCode, program.Stdout, string.Empty)), Tool));

    protected override Task<CliUnderTest> ArrangeMissingAsync()
    {
        var fake = new FakeCliRunner();
        _ = fake.WontStart.Add(Tool);
        return Task.FromResult(new CliUnderTest(fake, Tool));
    }
}
