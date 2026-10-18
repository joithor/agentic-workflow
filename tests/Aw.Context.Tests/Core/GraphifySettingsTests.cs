using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

public sealed class GraphifySettingsTests
{
    private static Result<ContextSettings> Read(string graphify) =>
        SettingsReader.Read(JsonDocument.Parse("{\"extract\":{\"graphify\":" + graphify + "}}").RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void Without_the_keys_the_limits_are_generous_defaults()
    {
        var expected = (60, 2048, 16, 256);

        foreach (var limits in new[] { SettingsReader.Read(section: null).Value.Graphify, Read("{}").Value.Graphify })
        {
            Assert.Equal(expected, (limits.TimeoutMinutes, limits.MaxSnapshotMB, limits.MaxFileMB, limits.MaxGraphMB));
        }

        Assert.Equal(2048L * 1024 * 1024, ClusterLimits.Default.MaxSnapshotBytes);
        Assert.Equal(16L * 1024 * 1024, ClusterLimits.Default.MaxFileBytes);
        Assert.Equal(256L * 1024 * 1024, ClusterLimits.Default.MaxGraphBytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Explicit_values_are_read_and_a_missing_one_keeps_its_default()
    {
        var limits = Read("""{"timeoutMinutes":5,"maxFileMB":4}""").Value.Graphify;

        Assert.Equal((5, 2048, 4, 256), (limits.TimeoutMinutes, limits.MaxSnapshotMB, limits.MaxFileMB, limits.MaxGraphMB));
        Assert.Equal(4L * 1024 * 1024, limits.MaxFileBytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Zero_means_unlimited_for_each_limit()
    {
        var limits = Read("""{"timeoutMinutes":0,"maxSnapshotMB":0,"maxFileMB":0,"maxGraphMB":0}""").Value.Graphify;

        Assert.Equal(0, limits.TimeoutMinutes);
        Assert.Null(limits.MaxSnapshotBytes);
        Assert.Null(limits.MaxFileBytes);
        Assert.Null(limits.MaxGraphBytes);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"timeoutMinutes":-1}""", "context.extract.graphify.timeoutMinutes:")]
    [InlineData("""{"maxSnapshotMB":1.5}""", "context.extract.graphify.maxSnapshotMB:")]
    [InlineData("""{"maxFileMB":-16}""", "context.extract.graphify.maxFileMB:")]
    [InlineData("""{"maxGraphMB":0.5}""", "context.extract.graphify.maxGraphMB:")]
    [InlineData("""{"maxGraphMB":99999999999}""", "context.extract.graphify.maxGraphMB:")]
    public void A_negative_fractional_or_absurd_value_is_SND_CONTEXT_133_at_its_key_path(string graphify, string prefix)
    {
        var settings = Read(graphify);

        Assert.True(settings.IsError);
        Assert.Equal(ContextErrors.GraphifyLimitInvalid.Code, settings.Error.Code);
        Assert.StartsWith(prefix, settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_longest_timeout_is_70000_minutes_because_a_timer_takes_no_more()
    {
        Assert.Equal(70_000, Read("""{"timeoutMinutes":70000}""").Value.Graphify.TimeoutMinutes);
        Assert.True(TimeSpan.FromMinutes(70_000).TotalMilliseconds < uint.MaxValue - 1);   // CancelAfter's own ceiling

        var over = Read("""{"timeoutMinutes":70001}""");

        Assert.Equal(ContextErrors.GraphifyLimitInvalid.Code, over.Error.Code);
        Assert.StartsWith("context.extract.graphify.timeoutMinutes: is a whole number of minutes, 0 to 70000", over.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"timeoutMinutes":"60"}""")]
    [InlineData("""{"timeout":60}""")]
    public void A_value_that_is_not_a_number_or_a_key_that_does_not_exist_is_refused(string graphify) =>
        Assert.True(Read(graphify).IsError);
}
