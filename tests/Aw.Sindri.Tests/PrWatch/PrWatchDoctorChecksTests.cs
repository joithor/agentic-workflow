using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.PrWatch;

namespace Aw.Sindri.Tests.PrWatch;

/// <summary>Doctor's 'pr-watch' check (PR babysitter spec API15): the watcher's last run, as its row in the services view says.</summary>
public sealed class PrWatchDoctorChecksTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_watcher_that_has_not_run_is_ok()
    {
        var check = Assert.Single(await new PrWatchDoctorChecks(new ServiceViews(row: null)).Run(TestContext.Current.CancellationToken));

        Assert.Equal(("pr-watch", CheckState.Ok), (check.Name, check.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_watcher_whose_last_run_was_ok_is_ok()
    {
        var check = Assert.Single(await new PrWatchDoctorChecks(new ServiceViews(Row(ServiceOutcome.Ok, code: null))).Run(TestContext.Current.CancellationToken));

        Assert.Equal(CheckState.Ok, check.State);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-FORGE-001", CheckState.Warn, "no forge is configured")]
    [InlineData("SND-FORGE-002", CheckState.Fail, "refused")]
    [InlineData("SND-FORGE-004", CheckState.Warn, "three polls")]
    [InlineData("SND-SVC-001", CheckState.Warn, "SND-SVC-001")]
    public async Task A_failed_run_is_named_by_its_code_with_a_fix(string code, CheckState state, string said)
    {
        var check = Assert.Single(await new PrWatchDoctorChecks(new ServiceViews(Row(ServiceOutcome.Failed, code))).Run(TestContext.Current.CancellationToken));

        Assert.Equal(state, check.State);
        Assert.Contains(said, check.Detail, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(check.Fix));
    }

    private static ServiceRow Row(ServiceOutcome outcome, string? code) =>
        new(ServiceName.From("pr-watch"), new LastRun(KnownInstants.Epoch, outcome, code, KnownInstants.Epoch), Paused: false, Sequence: 1);

    private sealed class ServiceViews(ServiceRow? row) : IViewReader
    {
        public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
            where TRow : class =>
            ValueTask.FromResult<Result<TRow?>>(view == ViewNames.Services && key == "pr-watch" ? row as TRow : null);

        public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
