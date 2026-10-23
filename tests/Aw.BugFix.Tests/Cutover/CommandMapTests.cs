using System.Reflection;
using Aw.BugFix.Host.Commands;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cutover;

/// <summary>
/// Cutover spec §4.2 as a list: every skill step is a C# verb, and every row the spec marks equivalent names the existing test that
/// pins it. A renamed or deleted test fails here, so the row can't silently lose its proof.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class CommandMapTests
{
    // Skill step (SKILL.md line) → C# verb → the test that pins it.
    public static TheoryData<string, string, string, string> EquivalentRows => new()
    {
        { "Resume (46)", "resume", "EvaluateTests", "Resume_names_the_next_step_in_each_phase_and_status_shows_the_state" },
        { "Intake (70)", "init", "IntakeTests", "Init_writes_intake_state_for_a_valid_ticket" },
        { "Investigate (76)", "advance investigate", "IntakeTests", "Advance_investigate_snapshots_the_root_cause_and_hypotheses" },
        { "Reproduce test (114)", "run-test", "ReproduceTests", "Run_test_registers_a_record_of_the_run_at_head" },
        { "Reproduce test (114), stricter", "run-test", "ReproduceTests", "Run_test_refuses_a_command_that_could_pass_without_running_the_check" },
        { "Reproduce (119)", "advance reproduce", "ReproduceTests", "Advance_reproduce_freezes_the_check_and_the_baseline" },
        { "Fix (131)", "start-attempt", "FixTests", "Start_attempt_opens_attempt_one_from_reproduce" },
        { "Fix (131), --abandon-judge", "start-attempt", "LeftoversAndShadowingTests", "A_verdict_that_arrives_after_its_candidate_was_left_behind_is_not_recorded" },
        { "Fix (167)", "record-candidate", "FixTests", "Record_candidate_records_the_commit_and_the_files_it_changed" },
        { "Evaluate (139)", "status", "EvaluateTests", "Resume_names_the_next_step_in_each_phase_and_status_shows_the_state" },
        { "Evaluate (185)", "record-run", "EvaluateTests", "Record_run_records_a_passing_run_and_points_at_judge" },
        { "Report (209)", "advance report", "EvaluateTests", "Advance_report_resolves_only_with_a_passing_run_and_a_resolved_verdict" },
        { "Report (209), unresolved", "advance report", "EvaluateTests", "Advance_report_unresolved_needs_exactly_one_outcome" },
    };

    [Theory]
    [MemberData(nameof(EquivalentRows))]
    public void An_equivalent_row_is_a_verb_of_the_table_pinned_by_a_test_that_exists(string row, string verb, string testClass, string test)
    {
        Assert.Contains($"\n  {verb} ", CommandTable.Usage, StringComparison.Ordinal);

        var type = typeof(CommandMapTests).Assembly.GetTypes().Single(t => t.Name == testClass);
        var method = type.GetMethod(test);
        Assert.True(method is not null, $"{row}: {testClass}.{test} is gone");
        Assert.True(method.GetCustomAttributes<FactAttribute>().All(static f => f.Skip is null), $"{row}: {testClass}.{test} is skipped");
    }

    [Theory]
    [InlineData("run-ui", "UiEvidenceCli", "cutover unit 2.1: Reproduce ui (101) is missing until the adapter exists")]
    [InlineData("judge", "JudgeCli", "cutover unit 2.1: Evaluate (187) is partial until the adapter exists")]
    public void The_two_stubbed_rows_have_their_verb_and_an_adapter_stub_waiting_for_unit_2_1(string verb, string adapter, string why)
    {
        Assert.Contains($"\n  {verb} ", CommandTable.Usage, StringComparison.Ordinal);
        Assert.True(typeof(Aw.BugFix.Infrastructure.BugFixSettings).Assembly.GetTypes().Any(t => t.Name == adapter), why);
    }
}
