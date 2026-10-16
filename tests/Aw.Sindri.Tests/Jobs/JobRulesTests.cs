using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The pure job rules (API P6 contract): the line bounds and the cancel check.</summary>
[Lane(TestLane.Unit)]
public sealed class JobRulesTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Keep_holds_the_last_200_lines()
    {
        IReadOnlyList<JobLine> lines = [];
        for (var n = 1; n <= 250; n++)
        {
            lines = JobLines.Keep(lines, new JobLine(n, string.Create(CultureInfo.InvariantCulture, $"line {n}"), _at));
        }

        Expect.Equal(JobLimits.ViewLines, lines.Count);
        Expect.Equal(51, lines[0].Round);
        Expect.Equal(250, lines[^1].Round);
    }

    [Theory]
    [InlineData(JobState.Running)]
    [InlineData(JobState.Succeeded)]
    [InlineData(JobState.Failed)]
    [InlineData(JobState.Cancelled)]
    public void Every_state_name_is_the_name_the_json_writes(JobState state)
    {
        var json = System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(state, SliceHarness.Json));

        Expect.Equal($"\"{JobStateNames.Of(state)}\"", json);   // the SQLite filter compares this name with the stored column
    }

    [Fact]
    public void Clean_cuts_a_line_to_200_characters_and_escapes_control_sequences()
    {
        var cleaned = JobLines.Clean(new PassThroughScrubber(), "\u001b[31mred\u001b[0m " + new string('x', 400));

        Expect.Equal(JobLimits.LineChars, cleaned.Length);   // exactly 200: an empty or short result would fail
        Assert.StartsWith("\\x1B[31mred\\x1B[0m xxx", cleaned, StringComparison.Ordinal);
        Assert.EndsWith("…", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', cleaned);
    }

    [Fact]
    public void CanCancel_is_404_for_no_row_409_for_an_ended_job_and_ok_while_running()
    {
        Expect.Equal(SindriErrors.JobNotFound.Code, JobRules.CanCancel(row: null).Error.Code);
        Expect.Equal(SindriErrors.JobConflict.Code, JobRules.CanCancel(A.Job().Ended(JobState.Succeeded).Build()).Error.Code);
        Assert.True(JobRules.CanCancel(A.Job().Build()).IsSuccess);
    }
}
