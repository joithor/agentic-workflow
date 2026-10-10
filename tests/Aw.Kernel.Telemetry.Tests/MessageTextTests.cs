using Aw.Kernel.Testing;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class MessageTextTests
{
    private static readonly string[] _items = ["a", "b"];
    private static readonly Func<string, bool> _isSecret = static n =>
        string.Equals(n.TrimStart('@', '$'), "Password", StringComparison.Ordinal) || string.Equals(n.TrimStart('@', '$'), "Token", StringComparison.Ordinal);

    [Fact]
    public void Without_a_secret_name_the_formatted_text_is_kept() =>
        Assert.Equal("formatted", MessageText.Render("formatted", Fields("{A}", ("A", 1)), _isSecret));

    [Fact]
    public void Holes_take_their_values_by_position_like_the_formatter()
    {
        var text = MessageText.Render("x", Fields("{A} then {A} with {Password}", ("A", 1), ("A", 2), ("Password", "pw")), _isSecret);

        Assert.Equal("1 then 2 with [REDACTED:secret-field]", text);
    }

    [Fact]
    public void Alignment_and_format_are_both_applied()
    {
        var text = MessageText.Render("x", Fields("[{Value,10:N2}] [{Name,-4}] {Token}", ("Value", 3.14159), ("Name", "ab"), ("Token", "t")), _isSecret);

        Assert.Equal("[      3.14] [ab  ] [REDACTED:secret-field]", text);
    }

    [Fact]
    public void Enumerables_and_null_render_like_the_formatter()
    {
        var text = MessageText.Render("x", Fields("{Items} {Missing} {Token}", ("Items", _items), ("Missing", null), ("Token", "t")), _isSecret);

        Assert.Equal("a, b (null) [REDACTED:secret-field]", text);
    }

    [Fact]
    public void Escaped_braces_stay_literal()
    {
        var text = MessageText.Render("x", Fields("{{literal}} {Token} }}", ("Token", "t")), _isSecret);

        Assert.Equal("{literal} [REDACTED:secret-field] }", text);
    }

    private static List<KeyValuePair<string, object?>> Fields(string template, params (string Key, object? Value)[] values) =>
        [.. values.Select(v => new KeyValuePair<string, object?>(v.Key, v.Value)), new(MessageText.TemplateKey, template)];
}
