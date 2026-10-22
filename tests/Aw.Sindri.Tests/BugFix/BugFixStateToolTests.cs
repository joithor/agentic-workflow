using Aw.Kernel.Testing;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Infrastructure.BugFix;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Tests.BugFix;

/// <summary>The bugfix-state adapter: the arguments it passes, the JSON it reads, the refusals it classifies and the failures it names.</summary>
[Lane(TestLane.Unit)]
public sealed class BugFixStateToolTests
{
    private sealed class Runner(params ToolRun[] runs) : IToolRunner
    {
        private readonly Queue<ToolRun> _runs = new(runs);

        public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, ToolRunOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add((fileName, arguments));
            return _runs.Dequeue();
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ToolRun Ok(string json) => new(ToolOutcome.Exited, 0, json);

    private static ToolRun Refused(string line, int exit = 3) => new(ToolOutcome.Exited, exit, "", line + "\n");

    private static (BugFixStateTool Tool, Runner Runner) Tool(params ToolRun[] runs)
    {
        var runner = new Runner(runs);
        return (new BugFixStateTool(runner, name => string.Equals(name, "AW_BUGFIX_BIN", StringComparison.Ordinal) ? "/bin/bugfix-state" : null), runner);
    }

    [Theory]
    [InlineData(AttemptMode.Whole, "A")]
    [InlineData(AttemptMode.Split, "C")]
    public async Task Start_attempt_passes_the_mode_letter_and_reads_the_attempt(AttemptMode mode, string letter)
    {
        var (tool, runner) = Tool(Ok($$"""{"phase":"fix","attempt":2,"mode":"{{letter}}"}"""));

        var answer = await tool.StartAttempt("/state/x", mode, Token);

        Assert.Equal("/bin/bugfix-state", runner.Calls[0].FileName);
        Assert.Equal(["start-attempt", "--state", "/state/x", "--mode", letter], runner.Calls[0].Arguments);
        Assert.Equal(new AttemptStarted(2, mode), answer.Value);
    }

    [Fact]
    public async Task The_attempt_cap_is_a_refusal_of_its_kind()
    {
        var (tool, _) = Tool(Refused("refused: attempt cap (3) reached; run advance report --unresolved (AW-BUGFIX-010)"));

        var answer = await tool.StartAttempt("/s", AttemptMode.Whole, Token);

        Assert.Equal(BugFixAnswerStatus.Refused, answer.Status);
        Assert.Equal(new BugFixRefusal("AW-BUGFIX-010", RefusalKind.AttemptCap, "attempt cap (3) reached; run advance report --unresolved"), answer.Refusal);
    }

    [Fact]
    public async Task Another_refusal_of_start_attempt_is_of_the_other_kind()
    {
        var (tool, _) = Tool(Refused("refused: candidate c1 has not been evaluated yet (AW-BUGFIX-010)"));

        var answer = await tool.StartAttempt("/s", AttemptMode.Whole, Token);

        Assert.Equal(RefusalKind.Other, answer.Refusal!.Kind);
    }

    [Fact]
    public async Task An_input_error_on_stderr_is_a_refusal_too()
    {
        var (tool, _) = Tool(Refused("error: no bug fix in the state directory: run init first (AW-BUGFIX-002)", exit: 1));

        var answer = await tool.StartAttempt("/s", AttemptMode.Whole, Token);

        Assert.Equal("AW-BUGFIX-002", answer.Refusal!.Code);
    }

    [Fact]
    public async Task Record_candidate_passes_the_source_and_reads_the_attempt_from_resume()
    {
        var (tool, runner) = Tool(
            Ok("""{"candidate":"c1","commit":"abc123","changedFiles":["src/a.cs","src/b.cs"]}"""),
            Ok("""{"phase":"evaluate","status":"active","attempt":2,"next":"recheck c1"}"""));

        var answer = await tool.RecordCandidate("/s", new CandidateSource("fix-1", "/work/fix-1", Hypothesis: 3, AllowTestChanges: true), Token);

        Assert.Equal(["record-candidate", "--state", "/s", "--branch", "fix-1", "--cwd", "/work/fix-1", "--hypothesis", "3", "--allow-test-changes"], runner.Calls[0].Arguments);
        Assert.Equal(["resume", "--state", "/s"], runner.Calls[1].Arguments);
        Assert.Equal("c1", answer.Value!.Candidate);
        Assert.Equal("abc123", answer.Value.Commit);
        Assert.Equal(2, answer.Value.Attempt);
        Assert.Equal(["src/a.cs", "src/b.cs"], answer.Value.ChangedFiles);
    }

    [Fact]
    public async Task A_plain_candidate_passes_no_hypothesis_and_no_approval()
    {
        var (tool, runner) = Tool(Ok("""{"candidate":"c1","commit":"abc","changedFiles":[]}"""), Ok("""{"attempt":1}"""));

        _ = await tool.RecordCandidate("/s", new CandidateSource("b", "/w"), Token);

        Assert.Equal(["record-candidate", "--state", "/s", "--branch", "b", "--cwd", "/w"], runner.Calls[0].Arguments);
    }

    [Fact]
    public async Task A_refused_record_candidate_does_not_ask_for_the_attempt()
    {
        var (tool, runner) = Tool(Refused("refused: commit abc is already candidate c1 (AW-BUGFIX-011)"));

        var answer = await tool.RecordCandidate("/s", new CandidateSource("b", "/w"), Token);

        Assert.Equal(BugFixAnswerStatus.Refused, answer.Status);
        _ = Assert.Single(runner.Calls);
    }

    [Fact]
    public async Task Recheck_runs_the_test_check_in_the_candidates_worktree_then_records_the_run()
    {
        var (tool, runner) = Tool(
            Ok("""{"evidence":"/s/runs/r1.json","outcome":"passed","exitCode":0,"commit":"abc"}"""),
            Ok("""{"candidate":"c1","passed":true,"next":"judge c1"}"""));

        var answer = await tool.Recheck("/s", "c1", "/work/fix-1", new FrozenCheck("test", "tests/bug.test.sh", ["bash", "tests/bug.test.sh"], "."), Token);

        Assert.Equal(["run-test", "--state", "/s", "--check", "tests/bug.test.sh", "--cwd", "/work/fix-1", "--", "bash", "tests/bug.test.sh"], runner.Calls[0].Arguments);
        Assert.Equal(["record-run", "c1", "--state", "/s", "--evidence", "/s/runs/r1.json"], runner.Calls[1].Arguments);
        Assert.Equal(new CheckRun("c1", Passed: true, ExitCode: 0, Evidence: "/s/runs/r1.json"), answer.Value);
    }

    [Fact]
    public async Task A_check_in_a_subdirectory_runs_there_and_a_failed_run_is_an_answer()
    {
        var (tool, runner) = Tool(
            Ok("""{"evidence":"/s/runs/r2.json","outcome":"failed","exitCode":1,"commit":"abc"}"""),
            Ok("""{"candidate":"c1","passed":false,"next":"start-attempt"}"""));

        var answer = await tool.Recheck("/s", "c1", "/work/fix-1", new FrozenCheck("test", "t.sh", ["bash", "t.sh"], "web"), Token);

        Assert.Equal("/work/fix-1/web", runner.Calls[0].Arguments[6]);
        Assert.Equal(new CheckRun("c1", Passed: false, ExitCode: 1, Evidence: "/s/runs/r2.json"), answer.Value);
    }

    [Fact]
    public async Task A_ui_check_runs_through_run_ui()
    {
        var (tool, runner) = Tool(
            Ok("""{"evidence":"/s/runs/r3.json","outcome":"passed","exitCode":0,"commit":"abc"}"""),
            Ok("""{"candidate":"c1","passed":true,"next":"judge c1"}"""));

        _ = await tool.Recheck("/s", "c1", "/work/fix-1", new FrozenCheck("ui", "ui/bug.json", [], "."), Token);

        Assert.Equal(["run-ui", "--state", "/s", "--check", "ui/bug.json", "--cwd", "/work/fix-1"], runner.Calls[0].Arguments);
    }

    [Fact]
    public async Task A_refused_run_is_not_recorded()
    {
        var (tool, runner) = Tool(Refused("refused: the work tree has uncommitted changes (AW-BUGFIX-020)"));

        var answer = await tool.Recheck("/s", "c1", "/w", new FrozenCheck("test", "t", ["t"], "."), Token);

        Assert.Equal("AW-BUGFIX-020", answer.Refusal!.Code);
        _ = Assert.Single(runner.Calls);
    }

    [Fact]
    public async Task Judge_reads_the_decision_reason_and_reasons()
    {
        var (tool, runner) = Tool(Ok("""{"candidate":"c1","decision":"escalated","decisionId":null,"reasonCode":"low-confidence","reasons":["a","b"],"status":"needs-human"}"""));

        var answer = await tool.Judge("/s", "c1", Token);

        Assert.Equal(["judge", "c1", "--state", "/s"], runner.Calls[0].Arguments);
        Assert.Equal(new ResolutionVerdict("c1", JudgeDecision.Escalated, "low-confidence", ["a", "b"]).Candidate, answer.Value!.Candidate);
        Assert.Equal(JudgeDecision.Escalated, answer.Value.Decision);
        Assert.Equal("low-confidence", answer.Value.ReasonCode);
        Assert.Equal(["a", "b"], answer.Value.Reasons);
    }

    [Theory]
    [InlineData("resolved", JudgeDecision.Resolved)]
    [InlineData("partial", JudgeDecision.Partial)]
    [InlineData("unresolved", JudgeDecision.Unresolved)]
    public async Task Each_decision_name_is_read(string name, JudgeDecision decision)
    {
        var (tool, _) = Tool(Ok($$"""{"candidate":"c1","decision":"{{name}}","reasonCode":"r","reasons":[],"status":"active"}"""));

        Assert.Equal(decision, (await tool.Judge("/s", "c1", Token)).Value!.Decision);
    }

    [Fact]
    public async Task A_candidate_without_a_passing_run_is_a_refusal_of_its_kind_and_a_second_ask_is_not()
    {
        var (tool, _) = Tool(
            Refused("refused: c1 has no passing run; judge only runs after the check passes (AW-BUGFIX-015)"),
            Refused("refused: c1 was already judged (resolved); a verdict is never re-asked (AW-BUGFIX-015)"));

        Assert.Equal(RefusalKind.NoPassingRun, (await tool.Judge("/s", "c1", Token)).Refusal!.Kind);
        Assert.Equal(RefusalKind.Other, (await tool.Judge("/s", "c1", Token)).Refusal!.Kind);
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("TimedOut")]
    [InlineData("Cancelled")]
    public async Task A_tool_that_did_not_exit_is_unavailable_with_the_fix(string outcome)
    {
        var (tool, _) = Tool(new ToolRun(Enum.Parse<ToolOutcome>(outcome), -1, ""));

        var answer = await tool.StartAttempt("/s", AttemptMode.Whole, Token);

        Assert.Equal(BugFixAnswerStatus.Unavailable, answer.Status);
        Assert.Contains("AW_BUGFIX_BIN", answer.Unavailable, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, "", "usage: --mode must be A, B or C")]
    [InlineData(0, "not json", "")]
    [InlineData(3, "", "something unrelated broke")]
    public async Task A_usage_error_unreadable_output_or_an_unparseable_refusal_is_unavailable(int exit, string output, string error)
    {
        var (tool, _) = Tool(new ToolRun(ToolOutcome.Exited, exit, output, error));

        var answer = await tool.StartAttempt("/s", AttemptMode.Whole, Token);

        Assert.Equal(BugFixAnswerStatus.Unavailable, answer.Status);
    }

    [Fact]
    public async Task The_program_is_bugfix_state_on_the_path_when_no_variable_is_set()
    {
        var runner = new Runner(Ok("""{"attempt":1}"""));

        _ = await new BugFixStateTool(runner, static _ => null).StartAttempt("/s", AttemptMode.Whole, Token);

        Assert.Equal("bugfix-state", runner.Calls[0].FileName);
    }
}
