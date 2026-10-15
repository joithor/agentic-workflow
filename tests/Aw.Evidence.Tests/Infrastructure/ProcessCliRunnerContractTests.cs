using Aw.Evidence.Infrastructure.Processes;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The real runner over stub programs.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessCliRunnerContractTests : CliRunnerContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override Task<CliUnderTest> ArrangeAsync(CliProgram program)
    {
        var tool = StubCommand.Write(_root, "tool", "printf '%s' '" + program.Stdout + "'\nexit " + program.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Task.FromResult(new CliUnderTest(new ProcessCliRunner(TimeSpan.FromSeconds(20)), tool));
    }

    protected override Task<CliUnderTest> ArrangeMissingAsync() =>
        Task.FromResult(new CliUnderTest(new ProcessCliRunner(TimeSpan.FromSeconds(20)), _root.Combine("absent")));

    public void Dispose() => _root.Dispose();
}
