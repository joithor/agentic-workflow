using Aw.Context.Core;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Results;
using Aw.Context.Features.EvalProbes;
using Aw.Context.Tests.Eval;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.EvalProbes;

public sealed class EvalProbesServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private static CancellationToken None => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_baseline_run_the_report_is_written_and_does_not_regress()
    {
        var world = new World();

        var run = await world.Run(Hit);

        Assert.Equal((null, false, true), (run.Value.Report.Baseline, run.Value.Report.Regressed, run.Value.Report.Totals.PrecisionAt5 > 0));
        Assert.Equal(("/probes/run-1.json", run.Value.Report), (run.Value.Path, world.Written.Single()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_worse_run_of_the_same_file_hash_regresses_against_the_baseline_run()
    {
        var world = new World();
        var first = await world.Run(Hit);
        world.Clock.Advance(TimeSpan.FromMinutes(1));

        var second = await world.Run(Miss);

        Assert.Equal(first.Value.Report.Totals, second.Value.Report.Baseline);
        Assert.True(second.Value.Report.Regressed);
        Assert.Equal(2, world.Written.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_with_a_failed_probe_is_returned_incomplete_and_not_written()
    {
        var world = new World { TwoProbes = true };
        static ValueTask<Result<ContextResult>> Ask(ProbeCase probe, CancellationToken ct) =>
            probe.Id.Value == "p2" ? ValueTask.FromResult<Result<ContextResult>>(ContextErrors.SessionsDisabled.With("off")) : Hit(probe, ct);

        var run = await world.Run(Ask);

        Assert.True(run.Value.Report.Incomplete);
        Assert.Null(run.Value.Path);
        Assert.Empty(world.Written);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_in_which_every_probe_failed_is_incomplete_names_them_and_is_not_written()
    {
        var world = new World { TwoProbes = true };

        var run = await world.Run((_, _) => ValueTask.FromResult<Result<ContextResult>>(ContextErrors.SessionsDisabled.With("off")));

        Assert.True(run.IsSuccess, run.IsError ? run.Error.Message : null);
        Assert.True(run.Value.Incomplete);
        Assert.Equal(["p1", "p2"], run.Value.Failed.Select(i => i.Value), StringComparer.Ordinal);
        Assert.Null(run.Value.Path);
        Assert.Empty(world.Written);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_regressed_run_is_not_the_baseline_of_the_next_one()
    {
        var world = new World();
        var first = await world.Run(Hit);
        world.Clock.Advance(TimeSpan.FromMinutes(1));
        _ = await world.Run(Miss);
        world.Clock.Advance(TimeSpan.FromMinutes(1));

        var again = await world.Run(Miss);

        Assert.Equal(first.Value.Report.Totals, again.Value.Report.Baseline);
        Assert.True(again.Value.Report.Regressed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_file_passes_through_as_the_error_and_nothing_is_asked_or_written()
    {
        var world = new World { Refusal = ContextErrors.ProbeFileInvalid.With("probes[0]: bad") };
        var asked = 0;

        var run = await world.Run((p, ct) => { asked++; return Hit(p, ct); });

        Assert.Equal((ContextErrors.ProbeFileInvalid.Code, 0, 0), (run.Error.Code, asked, world.Written.Count));
    }

    private static ValueTask<Result<ContextResult>> Hit(ProbeCase probe, CancellationToken ct) =>
        ValueTask.FromResult<Result<ContextResult>>(Answers.Of(("graph", Answers.Item("feature:x"))));

    private static ValueTask<Result<ContextResult>> Miss(ProbeCase probe, CancellationToken ct) =>
        ValueTask.FromResult<Result<ContextResult>>(Answers.Of());

    private sealed class World : IProbeFileReader, IProbeReportStore
    {
        private static readonly ProbeFile _file = new("/probes.json", "hash-1", new ProbeSet(Probes: [Answers.Probe("p1", nodes: ["feature:x"])]));
        private static readonly ProbeFile _two = new("/probes.json", "hash-1", new ProbeSet(Probes: [Answers.Probe("p1", nodes: ["feature:x"]), Answers.Probe("p2", nodes: ["feature:x"])]));

        public FakeTimeProvider Clock { get; } = new(startDateTime: _now);
        public List<ProbeReport> Written { get; } = [];
        public Error? Refusal { get; init; }
        public bool TwoProbes { get; init; }

        public ValueTask<Result<ProbeRun>> Run(Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> ask) =>
            new EvalProbesService(this, this, Clock).Handle(new EvalProbesRequest(File: null, ask), None);

        public ValueTask<Result<ProbeFile>> Read(string? path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Refusal is { } refused ? (Result<ProbeFile>)refused : TwoProbes ? _two : _file);

        public ValueTask<Result<ProbeReport?>> Baseline(string probeFileHash, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ProbeReport?>>(Written.LastOrDefault(r => r.ProbeFileHash == probeFileHash && r.IsBaseline));

        public ValueTask<Result<string>> Write(ProbeReport report, DateTimeOffset runAt, CancellationToken cancellationToken)
        {
            Written.Add(report);
            return ValueTask.FromResult<Result<string>>($"/probes/run-{Written.Count}.json");
        }
    }
}
