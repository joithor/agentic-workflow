using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Usage;
using Aw.Evidence.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.Steps;

public sealed class StepRunnerTests
{
    private static readonly BrowserSessionOptions _options = new(new ResolvedHost("http://localhost:3000", HostKind.Local), new ViewportSize(1280, 800), RecordDirectory: null, UserAgentOverride: null);

    private static FakeBrowserSession Session(Action<FakePage> arrange)
    {
        var page = new FakePage();
        arrange(page);
        return new FakeBrowserSession(page, _options);
    }

    private static async Task<(StepStatus Status, InvocationLog Log)> Run(FakeBrowserSession session, ScriptStep step, FakeJudge judge, StepRunOptions? options = null)
    {
        var log = new InvocationLog();
        var status = await new StepRunner(judge, new FakeTimeProvider()).RunAsync(session, step, options ?? StepRunOptions.Run, log, CancellationToken.None);
        return (status, log);
    }

    private static SelectorRepair Choose(int index) => new(RepairDecision.Repaired, index, Usage: null);

    [Fact, Trait("Lane", "unit")]
    public async Task A_click_whose_expected_state_holds_passes()
    {
        var session = Session(p =>
        {
            _ = p.TestIds.Add("open");
            p.OnClick["testId:open"] = static page => page.Texts.Add("Done");
        });

        var (status, log) = await Run(session, ScriptBuilder.Click("open"), FakeJudge.Unavailable);

        Assert.Equal(StepStatus.Passed, status);
        Assert.Empty(log.All);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_action_that_runs_but_misses_its_expectation_fails_and_is_never_repaired()
    {
        var judge = new FakeJudge().Repairs(Choose(0));
        var session = Session(static p => p.TestIds.Add("open"));

        var (status, _) = await Run(session, ScriptBuilder.Click("open"), judge);

        Assert.Equal(StepStatus.Failed, status);
        Assert.Empty(judge.RepairRequests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_broken_click_repaired_onto_a_visible_candidate_passes_and_is_recorded()
    {
        var judge = new FakeJudge().Repairs(new SelectorRepair(RepairDecision.Repaired, 3, new InvocationUsage("haiku", 120, 8)));
        var session = Session(p =>
        {
            p.Candidates.Add(new Candidate(3, "button", "Open", TestId.From("open-v2"), "Open"));
            p.OnClick["candidate:3"] = static page => page.Texts.Add("Done");
        });

        var (status, log) = await Run(session, ScriptBuilder.Click("open"), judge);

        Assert.Equal(StepStatus.Passed, status);
        Assert.Equal("open", judge.RepairRequests[0].BrokenSelector);
        Assert.Equal("click open; expected: text \"Done\" visible", judge.RepairRequests[0].Step);
        var invocation = Assert.Single(log.All);
        Assert.Equal((InvocationPhase.SelectorRepair, true, "haiku", 120L), (invocation.Phase, invocation.Ok, invocation.Model, invocation.InputTokens));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unavailable_judge_leaves_the_step_broken_after_two_attempts()
    {
        var judge = FakeJudge.Unavailable;
        var session = Session(static p => p.Candidates.Add(new Candidate(0, "button", "Open", TestId: null, "Open")));

        var (status, log) = await Run(session, ScriptBuilder.Click("open"), judge);

        Assert.Equal(StepStatus.Broken, status);
        Assert.Equal(RepairRules.MaxAttempts, judge.RepairRequests.Count);
        Assert.All(log.All, static i => Assert.False(i.Ok));
        Assert.All(log.All, static i => Assert.Null(i.InputTokens));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_proposal_naming_no_visible_candidate_is_not_tried()
    {
        var judge = new FakeJudge().Repairs(Choose(9), Choose(9));
        var session = Session(static p => p.Candidates.Add(new Candidate(0, "button", "Open", TestId: null, "Open")));

        var (status, _) = await Run(session, ScriptBuilder.Click("open"), judge);

        Assert.Equal(StepStatus.Broken, status);
        Assert.DoesNotContain(session.Calls, static c => c.StartsWith("click candidate", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fill_skips_a_proposal_that_is_not_fillable()
    {
        var judge = new FakeJudge().Repairs(Choose(0), Choose(1));
        var session = Session(p =>
        {
            p.Candidates.Add(new Candidate(0, "button", "Go", TestId: null, "Go"));
            p.Candidates.Add(new Candidate(1, "textbox", "Search", TestId.From("q"), Text: null));
        });

        var (status, _) = await Run(session, ScriptBuilder.Fill("search", "St"), judge);

        Assert.Equal(StepStatus.Broken, status);
        Assert.Equal(["fill testId:search", "fill candidate:1"], session.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_candidate_whose_element_shifted_after_listing_is_refused()
    {
        var judge = new FakeJudge().Repairs(Choose(0), Choose(0));
        var session = Session(static p =>
        {
            p.Candidates.Add(new Candidate(0, "button", "Open", TestId.From("open-v2"), "Open"));
            p.OnClick["candidate:0"] = static page => page.Texts.Add("Done");
            p.OnEnumerate = static page => page.Candidates[0] = page.Candidates[0] with { TestId = TestId.From($"{page.Candidates[0].TestId?.Value}-moved") };
        });

        var (status, _) = await Run(session, ScriptBuilder.Click("open"), judge);

        Assert.Equal(StepStatus.Broken, status);
        Assert.DoesNotContain("Done", session.Page.Texts);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(StepAction.ExpectVisible, "Missing")]
    [InlineData(StepAction.Goto, "//evil.example")]
    public async Task Only_click_and_fill_are_ever_repaired(StepAction action, string target)
    {
        var judge = new FakeJudge().Repairs(Choose(0));
        var session = Session(static p => p.Candidates.Add(new Candidate(0, "a", "x", TestId: null, "x")));

        var (status, _) = await Run(session, new ScriptStep(action, target, new TextVisible("x")), judge);

        Assert.Equal(StepStatus.Broken, status);
        Assert.Empty(judge.RepairRequests);
        Assert.DoesNotContain(session.Calls, static c => c.StartsWith("goto", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parity_steps_never_ask_for_a_repair()
    {
        var judge = new FakeJudge().Repairs(Choose(0));
        var session = Session(static p => p.Candidates.Add(new Candidate(0, "button", "Open", TestId: null, "Open")));

        var (status, _) = await Run(session, ScriptBuilder.Click("open"), judge, StepRunOptions.Parity(TimeSpan.FromSeconds(10)));

        Assert.Equal(StepStatus.Broken, status);
        Assert.Empty(judge.RepairRequests);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("text-visible", true)]
    [InlineData("text-absent", false)]
    [InlineData("testid-visible", true)]
    [InlineData("url-path", true)]
    [InlineData("input-value", true)]
    public async Task Each_expected_state_kind_is_checked_against_the_page(string kind, bool holds)
    {
        var session = Session(static p =>
        {
            _ = p.Texts.Add("Saved");
            _ = p.TestIds.Add("q");
            p.Inputs["q"] = "St";
            p.Path = "/done";
        });
        ExpectedState expected = kind switch
        {
            "text-visible" => new TextVisible("Saved"),
            "text-absent" => new TextAbsent("Saved"),
            "testid-visible" => new TestIdVisible(TestId.From("q")),
            "url-path" => new UrlPath("/done"),
            _ => new InputValue(TestId.From("q"), "St"),
        };

        Assert.Equal(holds, await StepRunner.ExpectationHolds(session, expected, TimeSpan.FromSeconds(3), CancellationToken.None));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_goto_navigates_on_the_host_and_checks_the_path()
    {
        var session = Session(static _ => { });

        var (status, _) = await Run(session, ScriptBuilder.Goto("/hospitals?tab=all"), FakeJudge.Unavailable);

        Assert.Equal(StepStatus.Passed, status);
        Assert.Equal(["goto /hospitals?tab=all"], session.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("input", true)]
    [InlineData("textbox", true)]
    [InlineData("textarea", true)]
    [InlineData("searchbox", true)]
    [InlineData("button", false)]
    [InlineData(null, false)]
    public void A_fill_fits_only_fillable_roles(string? role, bool fits)
    {
        var candidate = new Candidate(0, role, AccessibleName: null, TestId: null, Text: null);

        Assert.Equal(fits, RepairRules.Fits(StepAction.Fill, candidate));
        Assert.True(RepairRules.Fits(StepAction.Click, candidate));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_no_good_candidate_decision_resolves_to_nothing() =>
        Assert.Null(RepairRules.Resolve(new SelectorRepair(RepairDecision.NoGoodCandidate, 0, Usage: null), [new Candidate(0, "a", AccessibleName: null, TestId: null, Text: null)]));
}
