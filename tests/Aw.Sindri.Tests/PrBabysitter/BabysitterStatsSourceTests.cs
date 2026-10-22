using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Stats;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.GetRecipeStats.StatRows;

namespace Aw.Sindri.Tests.PrBabysitter;

/// <summary>Reads the ledger rows and each babysitter instance's merge record: a record that can't be read is one record error, not a failed call.</summary>
[Lane(TestLane.Unit)]
public sealed class BabysitterStatsSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_record_that_cannot_be_read_is_counted_as_one_error_and_the_figures_still_come()
    {
        var rows = new Rows([Start("T-1/pr-babysitter/1", Ago(1), state: "intake", recipe: "workflow.pr-babysitter"), Start("T-2/pr-babysitter/1", Ago(1), state: "intake", recipe: "workflow.pr-babysitter")]);
        var source = new BabysitterStatsSource(rows, new Records(unreadable: "T-2/pr-babysitter/1"), new FakeTimeProvider(Now));

        var stats = await source.Of(StatsWindow.ThirtyDays, Token);

        Assert.Equal((2, 1), (stats.Value.Pulls, stats.Value.RecordErrors));
    }

    [Fact]
    public async Task A_failed_ledger_read_is_the_error()
    {
        var source = new BabysitterStatsSource(new Rows(error: true), new Records(unreadable: null), new FakeTimeProvider(Now));

        Assert.True((await source.Of(StatsWindow.ThirtyDays, Token)).IsError);
    }

    private sealed class Rows(IReadOnlyList<StatEvent>? events = null, bool error = false) : IWorkflowStatRows
    {
        public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => ValueTask.FromResult<Result<long>>(1);

        public ValueTask<Result<IReadOnlyList<StatEvent>>> Read(CancellationToken cancellationToken)
        {
            if (error)
            {
                return ValueTask.FromResult<Result<IReadOnlyList<StatEvent>>>(KernelErrors.NotFound.With("no ledger"));
            }

            return ValueTask.FromResult(Results.Success<IReadOnlyList<StatEvent>>(events ?? []));
        }
    }

    private sealed class Records(string? unreadable) : IMergeRecordReader
    {
        public ValueTask<Result<MergeRecordRows>> Read(string address, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<MergeRecordRows>>(address == unreadable ? KernelErrors.NotFound.With("unreadable") : MergeRecordRows.Empty);
    }
}
