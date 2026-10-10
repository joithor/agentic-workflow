using Aw.Kernel.Testing;
using Aw.Kernel.Telemetry.Testing;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class LogSinkContractCatchesBrokenSinksTests
{
    [Fact]
    public void A_sink_that_accepts_line_breaks_fails_the_contract()
    {
        var contract = new BrokenSinkContract();

        _ = Assert.Throws<ContractViolationException>(contract.A_line_with_a_line_break_is_refused);
    }

    [Fact]
    public void A_sink_that_drops_the_terminator_fails_the_contract()
    {
        var contract = new BrokenSinkContract();

        _ = Assert.Throws<ContractViolationException>(contract.Lines_arrive_in_order_each_ending_with_a_newline);
    }

    private sealed class BrokenSinkContract : LogSinkContract
    {
        protected override ILogSink CreateSink() => new BrokenSink();

        protected override string Delivered(ILogSink sink) => ((BrokenSink)sink).Text.ToString();
    }

    // Accepts anything and forgets the '\n' after each line.
    private sealed class BrokenSink : ILogSink
    {
        public System.Text.StringBuilder Text { get; } = new();

        public void WriteLine(string line) => Text.Append(line);

        public void Flush()
        {
        }
    }
}
