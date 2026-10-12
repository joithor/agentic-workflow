using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>UI spec §12.4's sentences that take a value.</summary>
[Lane(TestLane.Unit)]
public sealed class LabelsTests
{
    [Theory]
    [InlineData(0, "updated 0s ago")]
    [InlineData(2.9, "updated 2s ago")]
    [InlineData(59, "updated 59s ago")]
    [InlineData(60, "updated 1m ago")]
    [InlineData(3599, "updated 59m ago")]
    [InlineData(3600, "updated 1h ago")]
    [InlineData(-5, "updated 0s ago")]
    public void Updated_ago_counts_seconds_then_minutes_then_hours(double seconds, string words) =>
        Assert.Equal(words, Labels.UpdatedAgo(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void The_versions_line_names_both() => Assert.Equal("Sindri speaks API v2; this app speaks v1", Labels.Versions(2, 1));

    // I7 (§12.4): every Not running reason is a sentence from S1b's SND-UI-001/002 wording, capitalised, with no
    // internals in it; the detail (path, address, pid and epoch) is its own line.
    [Fact]
    public void Not_running_reasons_are_sentences() =>
        Assert.All([Labels.NoRecord, Labels.RecordUnreadable, Labels.StaleRecord, Labels.NothingAnswered], r =>
        {
            Assert.True(char.IsUpper(r[0]), r);
            Assert.DoesNotMatch("[0-9()]", r);
        });

    [Theory]
    [InlineData(69190, 4, 69190, 0, "recorded pid 69190 · epoch 4, answering epoch 0")]
    [InlineData(1, 7, 4242, 7, "recorded pid 1 · epoch 7, answering pid 4242 · epoch 7")]
    public void The_stale_record_detail_names_what_differs(int pid, long epoch, int answeringPid, long answeringEpoch, string detail) =>
        Assert.Equal(detail, Labels.StaleRecordDetail(pid, epoch, answeringPid, answeringEpoch));
}
