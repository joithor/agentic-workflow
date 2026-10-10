using Aw.Readiness.App.Features.CheckRepo;
using Aw.Readiness.Core;
using Aw.Readiness.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Readiness.Tests.CheckRepo;

public sealed class CheckRepoTests
{
    private const string Root = "/repo";
    private static readonly string[] _hostIds = ["BS-4", "SEC-1", "SEC-2"];
    private readonly FakeTimeProvider _clock = new(RepoHostContract.Now);
    private readonly InMemoryRunHistory _history = new();
    private readonly InMemoryRepoTree _tree = A.ReadyTemplate();
    private readonly FakeRepoHost _host = new(HostFactsFor.Scenario(HostScenario.Ready));

    private CheckRepoService Service() => new(new InMemoryRepoReader().With(Root, _tree), _host, _history, _clock);

    [Fact, Trait("Lane", "unit")]
    public async Task CheckRepo_reports_level_per_pillar()
    {
        var result = await Service().Handle(A.CheckRepoRequest().AtRoot(Root).Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var report = result.Value;
        Assert.Equal(CheckRepoResponse.CurrentSchemaVersion, report.SchemaVersion);
        Assert.Equal(5, report.Level);
        Assert.Equal(new ReadinessTarget(0, 4), report.Target);
        Assert.True(report.Holds);
        Assert.Equal(9, report.Pillars.Count);
        Assert.All(report.Pillars, p => Assert.NotNull(p.Level));
        Assert.Equal(34, report.Findings.Count);
        Assert.Equal(RepoHostContract.Now, report.RecordedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_run_records_a_baseline_and_the_next_reports_the_delta()
    {
        var first = await Service().Handle(A.CheckRepoRequest().AtRoot(Root).Build(), CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(1));
        _ = _tree.Without(".github/CODEOWNERS");

        var second = await Service().Handle(A.CheckRepoRequest().AtRoot(Root).Build(), CancellationToken.None);

        Assert.True(first.Value.Delta.IsFirstRun);
        Assert.False(second.Value.Delta.IsFirstRun);
        Assert.Equal(RepoHostContract.Now, second.Value.Delta.Since);
        var change = Assert.Single(second.Value.Delta.Changes);
        Assert.Equal(new StateChange(CriterionId.From("SEC-3"), CriterionState.Pass, CriterionState.Fail), change);
        Assert.Equal(2, _history.Runs.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Offline_skips_the_host_and_leaves_its_criteria_unknown()
    {
        var result = await Service().Handle(A.CheckRepoRequest().AtRoot(Root).Offline().Build(), CancellationToken.None);

        Assert.Equal(0, _host.Reads);
        Assert.All(result.Value.Findings.Where(f => _hostIds.Contains(f.Criterion.Id.Value, StringComparer.Ordinal)),
            f => Assert.Equal(CriterionState.Unknown, f.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_level_below_the_target_does_not_hold()
    {
        _ = _tree.Without(".devcontainer/devcontainer.json").Without(".env.example").Without(".pre-commit-config.yaml");
        var host = new FakeRepoHost(HostFactsFor.Scenario(HostScenario.Bare));
        var service = new CheckRepoService(new InMemoryRepoReader().With(Root, _tree), host, _history, _clock);

        var result = await service.Handle(A.CheckRepoRequest().AtRoot(Root).Build(), CancellationToken.None);

        Assert.Equal(2, result.Value.Level);
        Assert.False(result.Value.Holds);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_root_is_AW_READINESS_002_and_nothing_is_recorded()
    {
        var result = await Service().Handle(A.CheckRepoRequest().AtRoot("/elsewhere").Build(), CancellationToken.None);

        Assert.Equal("AW-READINESS-002", result.Error.Code);
        Assert.Empty(_history.Runs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_readiness_yaml_is_AW_READINESS_003_and_nothing_is_recorded()
    {
        _ = _tree.Without(ReadinessConfig.FileName);

        var result = await Service().Handle(A.CheckRepoRequest().AtRoot(Root).Build(), CancellationToken.None);

        Assert.Equal("AW-READINESS-003", result.Error.Code);
        Assert.Contains(ReadinessConfig.FileName, result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_history.Runs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unavailable_history_is_AW_READINESS_004()
    {
        _history.Unavailable = true;

        var result = await Service().Handle(A.CheckRepoRequest().AtRoot(Root).Build(), CancellationToken.None);

        Assert.Equal("AW-READINESS-004", result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_root_is_refused_by_the_validator()
    {
        var validator = new CheckRepoValidator();

        var result = validator.Validate(A.CheckRepoRequest().AtRoot(" ").Build());

        Assert.Equal("AW-READINESS-001", result.Error.Code);
        await Task.CompletedTask;
    }
}
