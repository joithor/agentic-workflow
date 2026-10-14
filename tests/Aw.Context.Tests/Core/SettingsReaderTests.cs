using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

public sealed class SettingsReaderTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void No_section_gives_the_spec_defaults()
    {
        var settings = SettingsReader.Read(section: null).Value;

        Assert.Equal((TimeSpan.FromSeconds(4), 32_000, 3, false), (settings.Deadline, settings.MaxBudget, settings.MinHits, settings.SessionsEnabled));
        Assert.Equal(ContextSettings.DefaultDocGlobs, settings.DocGlobs);
        Assert.Null(settings.Repos);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_key_is_read()
    {
        var settings = Read("""{"repos":["split-panes"],"docs":{"globs":["docs/**/*.md"]},"deadline":"500ms","maxBudget":6000,"minHits":1,"sessions":{"enabled":true}}""").Value;

        Assert.Equal((TimeSpan.FromMilliseconds(500), 6_000, 1, true), (settings.Deadline, settings.MaxBudget, settings.MinHits, settings.SessionsEnabled));
        Assert.Equal(["docs/**/*.md"], settings.DocGlobs, StringComparer.Ordinal);
        Assert.Equal(["split-panes"], settings.Repos!.Select(r => r.Value), StringComparer.Ordinal);   // why: set above
    }

    [Fact, Trait("Lane", "unit")]
    public void Extra_scrub_patterns_are_read_as_written()
    {
        var settings = Read("""{"scrub":{"extraPatterns":[{"id":"acme-customer-id","pattern":"ACME-[0-9]{6}"},{"id":"order-id"}]}}""").Value;

        Assert.Equal([new("acme-customer-id", "ACME-[0-9]{6}"), new("order-id", Pattern: null)], settings.ExtraPatterns);
        Assert.Empty(SettingsReader.Read(section: null).Value.ExtraPatterns);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"scrub":{"extraPatterns":[{"id":"a","pattern":"b"},null]}}""", "context.scrub.extraPatterns[1]:")]
    [InlineData("""{"scrub":{"extra":[]}}""", "context:")]
    [InlineData("""{"scrub":{"extraPatterns":[{"id":"a","pattern":"b","flags":"i"}]}}""", "context:")]
    [InlineData("""{"deadline":"soon"}""", "context.deadline:")]
    [InlineData("""{"deadline":"90s"}""", "context.deadline:")]
    [InlineData("""{"maxBudget":10}""", "context.maxBudget:")]
    [InlineData("""{"minHits":99}""", "context.minHits:")]
    [InlineData("""{"docs":{"globs":["../etc/*"]}}""", "context.docs.globs:")]
    [InlineData("""{"docs":{"globs":[]}}""", "context.docs.globs:")]
    [InlineData("""{"repos":[""]}""", "context.repos:")]
    [InlineData("""{"repos":[null]}""", "context.repos:")]
    [InlineData("""{"repos":["a/b"]}""", "context.repos:")]
    [InlineData("""{"docs":{"globs":[null]}}""", "context.docs.globs:")]
    [InlineData("""{"labeling":{"enabled":true}}""", "context.labeling:")]
    [InlineData("""{"sessions":{"enabled":true,"x":1}}""", "context:")]
    [InlineData("""[1]""", "context:")]
    public void A_bad_key_is_001_with_its_key_path(string json, string path)
    {
        var read = Read(json);

        Assert.Equal(ContextErrors.SettingsInvalid.Code, read.Error.Code);
        Assert.StartsWith(path, read.Error.Message, StringComparison.Ordinal);
    }
}
