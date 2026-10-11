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
}
