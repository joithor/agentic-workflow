using System.Globalization;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

public sealed class PublishSettingsTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void The_wait_for_the_index_defaults_to_15_minutes()
    {
        Assert.Equal(15, SettingsReader.Read(section: null).Value.Publish.WaitForIndexMinutes);
        Assert.Equal(15, Read("""{"publish":{}}""").Value.Publish.WaitForIndexMinutes);
        Assert.Equal(15, Read("""{"deadline":"4s"}""").Value.Publish.WaitForIndexMinutes);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(240)]
    public void The_wait_for_the_index_takes_0_to_240_minutes(int minutes)
    {
        var settings = Read("{\"publish\":{\"waitForIndexMinutes\":" + minutes.ToString(CultureInfo.InvariantCulture) + "}}");

        Assert.Equal(minutes, settings.Value.Publish.WaitForIndexMinutes);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("-1")]
    [InlineData("241")]
    [InlineData("1000000")]
    public void A_wait_outside_0_to_240_is_SND_CONTEXT_001_with_the_key_path(string value)
    {
        var settings = Read("{\"publish\":{\"waitForIndexMinutes\":" + value + "}}");

        Assert.Equal(ContextErrors.SettingsInvalid.Code, settings.Error.Code);
        Assert.StartsWith("context.publish.waitForIndexMinutes:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"15\"")]
    [InlineData("1.5")]
    public void A_wait_that_is_not_a_whole_number_is_SND_CONTEXT_001(string value) =>
        Assert.Equal(ContextErrors.SettingsInvalid.Code, Read("{\"publish\":{\"waitForIndexMinutes\":" + value + "}}").Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_publish_key_is_SND_CONTEXT_001()
    {
        var settings = Read("""{"publish":{"waitMinutes":5}}""");

        Assert.Equal(ContextErrors.SettingsInvalid.Code, settings.Error.Code);
    }
}
