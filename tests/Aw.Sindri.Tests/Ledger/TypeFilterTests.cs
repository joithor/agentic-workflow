using Aw.Sindri.Core;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.Ledger;

public sealed class TypeFilterTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("service.heartbeat", "service.heartbeat", true)]
    [InlineData("service.heartbeat", "service.ran", false)]
    [InlineData("service.*", "service.heartbeat", true)]
    [InlineData("service.*", "service", false)]
    [InlineData("service.*", "services.ran", false)]
    [InlineData("item.*", "item.state-changed", true)]
    [InlineData("a.b.*", "a.b.c.d", true)]
    [InlineData("process.started,service.*", "process.started", true)]
    [InlineData("process.started,service.*", "service.ran", true)]
    [InlineData("process.started,service.*", "process.stopped", false)]
    public void Typefilter_matches_exact_prefix_and_list_globs(string text, string type, bool expected)
    {
        var filter = TypeFilter.Parse(text);

        Assert.True(filter.IsSuccess);
        Assert.Equal(expected, filter.Value.Matches(type));
    }

    [Fact, Trait("Lane", "unit")]
    public void Typefilter_keeps_the_globs_in_the_order_given()
        => Assert.Equal(["process.started", "service.*"], TypeFilter.Parse("process.started,service.*").Value.Globs);

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(",")]
    [InlineData("service.")]
    [InlineData(".service")]
    [InlineData("service..ran")]
    [InlineData("Service.Ran")]
    [InlineData("service.*.ran")]
    [InlineData("*")]
    [InlineData("service*")]
    [InlineData("service.ran,")]
    [InlineData("service.ran, process.started")]
    [InlineData("service_ran")]
    [InlineData("100%")]
    public void Typefilter_refuses_a_malformed_glob_as_SND_CLI_003(string text)
    {
        var filter = TypeFilter.Parse(text);

        Assert.True(filter.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, filter.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\u001b[31mred")]
    [InlineData("service.*,\u001b]0;title\u0007")]
    public void Typefilter_echoes_a_refused_glob_without_control_characters(string text)
    {
        var message = TypeFilter.Parse(text).Error.Message;

        Assert.DoesNotContain(message, static c => char.IsControl(c));
        Assert.Contains("\\x1B", message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Typefilter_caps_the_echo_of_a_refused_glob_at_80_characters()
    {
        var message = TypeFilter.Parse(new string('X', 5_000)).Error.Message;

        Assert.True(message.Length < 200, message);
    }

    [Fact, Trait("Lane", "unit")]
    public void Typefilter_takes_16_globs_and_refuses_17()
    {
        static string List(int count) => string.Join(',', Enumerable.Range(0, count).Select(i => "t" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".*"));

        Assert.True(TypeFilter.Parse(List(16)).IsSuccess);
        Assert.Equal(SindriErrors.InvalidInput.Code, TypeFilter.Parse(List(17)).Error.Code);
    }
}
