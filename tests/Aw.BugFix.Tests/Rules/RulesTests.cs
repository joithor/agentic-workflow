using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Rules;

namespace Aw.BugFix.Tests.Rules;

public sealed class RulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_handoff_parser_reads_status_rows_files_and_the_root_cause_section()
    {
        const string markdown = "status: diagnosed\r\n\r\n## Hypotheses\r\n\r\n| # | Hypothesis | Cause-site files | Likelihood | Result |\r\n|---|---|---|---|---|\r\n| 1 | A | a.ts, b.ts | high | ruled-out |\r\n| 2 | B | `c d.ts` | low | confirmed |\r\nafter\r\n\r\n## Root Cause\r\n\r\nLine one.\r\nLine two.\r\n\r\n## Next\r\n";

        var handoff = World.Ok(HandoffParser.Parse(markdown));

        Assert.Equal("diagnosed", handoff.Status);
        Assert.Equal([["a.ts", "b.ts"], ["c d.ts"]], handoff.Hypotheses.Select(static h => h.Files));
        Assert.Equal([HypothesisResult.RuledOut, HypothesisResult.Confirmed], handoff.Hypotheses.Select(static h => h.Result));
        Assert.Equal("Line one.\nLine two.", handoff.RootCause);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("## Hypotheses\n", "no status: line")]
    [InlineData("status: diagnosed\n", "no ## Hypotheses section")]
    [InlineData("status: diagnosed\n## Hypotheses\n| # | h | f | l | r |\n|-|-|-|-|-|\n| x | h | f | l | confirmed |\n", "malformed hypotheses row")]
    [InlineData("status: diagnosed\n## Hypotheses\n| # | h | f | l | r |\n|-|-|-|-|-|\n| 1 | h | f | l | maybe |\n", "malformed hypotheses row")]
    public void The_handoff_parser_names_what_is_missing(string markdown, string reason)
    {
        var error = World.Refused(HandoffParser.Parse(markdown), BugFixErrors.HandoffRefused);

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/app.ts", "tests/bug.test.sh", false)]
    [InlineData("tests/bug.test.sh", "tests/bug.test.sh", true)]
    [InlineData("checks/helper.sh", "checks/bug.sh", true)]
    [InlineData("helper.sh", "bug.sh", false)]
    [InlineData("src/__snapshots__/a.snap", "t/bug.sh", true)]
    [InlineData("pkg/foo_test.go", "t/bug.sh", true)]
    [InlineData("Tests/LoginTest.java", "t/bug.sh", true)]
    [InlineData("src/Contest.cs", "t/bug.sh", false)]
    [InlineData("SRC/JEST.CONFIG.JS", "t/bug.sh", true)]
    [InlineData("web/tsconfig.app.json", "t/bug.sh", true)]
    [InlineData("scripts/run.sh", "t/bug.sh", true)]
    public void Protected_paths_cover_the_check_its_directory_tests_and_their_config(string file, string check, bool touched) =>
        Assert.Equal(touched, ProtectedPaths.Touched([file], check, ["scripts/run.sh"]).Count == 1);

    [Theory, Trait("Lane", "unit")]
    [InlineData("bash t.sh", null)]
    [InlineData("npx vitest run t.test.ts", null)]
    [InlineData("python3.12 -m pytest t.py", null)]
    [InlineData("node --eval 1 t.js", "inline code")]
    [InlineData("python3.12 -c 1 t.py", "inline code")]
    [InlineData("/bin/cat t.sh", "not `cat`")]
    [InlineData("[ -f t.sh ]", "not `[`")]
    [InlineData("env sh t.sh", "not `env`")]
    [InlineData("timeout 60 bash t.sh", "not `timeout`")]
    [InlineData("node -p 1 t.js", "inline code (`node -p`)")]
    [InlineData("node --print 1 t.js", "inline code")]
    [InlineData("perl -E 1 t.pl", "inline code (`perl -E`)")]
    [InlineData("php -r 1 t.php", "inline code (`php -r`)")]
    [InlineData("deno eval 1 t.ts", "inline code (`deno eval`)")]
    [InlineData("npx -c true t.test.ts", "inline code (`npx -c`)")]
    [InlineData("npx jest t.test.ts", null)]
    [InlineData("npx node -e 1 t.js", "inline code (`node -e`)")]
    [InlineData("npx --yes sh -c true t.sh", "inline code (`sh -c`)")]
    [InlineData("npm exec cat t.sh", "not `cat`")]
    [InlineData("uv run python -c 1 t.py", "inline code (`python -c`)")]
    [InlineData("npm test t.test.ts", null)]
    [InlineData("bash -e t.sh", null)]
    [InlineData("python -E t.py", null)]
    [InlineData("bash -xec true t.sh", "inline code (`bash -xec`)")]
    public void The_check_command_rule_refuses_non_runners_and_inline_code(string command, string? reason)
    {
        var refusal = CheckCommandRule.Refusal(command.Split(' '));

        if (reason is null)
        {
            Assert.Null(refusal);
        }
        else
        {
            Assert.Contains(reason, refusal, StringComparison.Ordinal);
        }
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(new[] { RunOutcome.Passed, RunOutcome.Passed }, RunOutcome.Passed)]
    [InlineData(new[] { RunOutcome.Broken, RunOutcome.Failed }, RunOutcome.Failed)]
    [InlineData(new[] { RunOutcome.Passed, RunOutcome.Broken }, RunOutcome.Broken)]
    [InlineData(new RunOutcome[0], RunOutcome.Broken)]
    public void A_ui_run_fails_on_any_failed_step_and_is_broken_on_broken_steps_alone(RunOutcome[] steps, RunOutcome expected) =>
        Assert.Equal(expected, new UiRun(steps, "sha", 0).Outcome);

    [Fact, Trait("Lane", "unit")]
    public void A_frozen_test_check_accepts_only_the_same_command_in_the_same_directory()
    {
        var check = new FrozenCheck(CheckKind.Test, "tests/bug.sh", "sha", ["bash", "tests/bug.sh"], Directory: string.Empty, ProgramSha256: "bash", CommandFiles: []);
        var run = new RunRecord(CheckKind.Test, RunOutcome.Passed, "c", Directory: string.Empty, "sha", ["bash", "tests/bug.sh"], ProgramSha256: "bash", 0, "/log");

        _ = World.Ok(check.Accepts(run));
        _ = World.Refused(check.Accepts(run with { Directory = "lib" }), BugFixErrors.RunRefused);
        _ = World.Refused(check.Accepts(run with { Command = ["bash", "./tests/bug.sh"] }), BugFixErrors.RunRefused);
        _ = World.Refused(check.Accepts(run with { Outcome = RunOutcome.Broken }), BugFixErrors.RunRefused);
        _ = World.Refused(check.Accepts(run with { ProgramSha256 = "another bash" }), BugFixErrors.RunRefused);
    }

    [Fact, Trait("Lane", "unit")]
    public void Command_files_include_flag_values_and_module_files()
    {
        var paths = CommandFilesRule.Paths(["node", "--require=./lib/setup.cjs", "-r./lib/r.js", "-m", "tools.runner", "t.js"]);

        Assert.Contains(("./lib/setup.cjs", false), paths);
        Assert.Contains(("./lib/r.js", true), paths);
        Assert.Contains(("tools/runner.py", true), paths);
        Assert.Contains(("tools/runner/__init__.py", true), paths);
        Assert.Contains(("t.js", false), paths);
    }
}
