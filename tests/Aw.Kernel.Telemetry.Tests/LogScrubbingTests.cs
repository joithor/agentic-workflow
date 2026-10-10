using Aw.Kernel.Testing;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class LogScrubbingTests
{
    [Fact]
    public void A_secret_in_the_message_is_redacted()
    {
        using var h = new Harness();

        h.Logger().Emit(LogLevel.Information, "calling with " + Secrets.Bearer);

        var message = Assert.Single(h.Lines()).GetProperty("message").GetString();
        Assert.Contains("[REDACTED:", message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secrets.Body, h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_secret_passed_as_a_structured_value_is_redacted_in_the_message_and_the_fields()
    {
        using var h = new Harness();

        var logger = h.Logger();

        Log.EnvAndToken(logger, Secrets.EnvPair, Secrets.GithubToken);

        _ = Assert.Single(h.Lines());
        Assert.DoesNotContain(Secrets.Body, h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_with_a_secret_name_is_withheld_whatever_its_value()
    {
        using var h = new Harness();

        var logger = h.Logger();

        Log.Login(logger, "hunter2-not-a-shape");

        var line = Assert.Single(h.Lines());
        Assert.Equal("[REDACTED:secret-field]", line.GetProperty("fields").GetProperty("Password").GetString());
        Assert.DoesNotContain("hunter2", h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_secret_in_an_exception_message_is_redacted()
    {
        using var h = new Harness();

        var logger = h.Logger();

        Log.Failed(logger, new InvalidOperationException("rejected " + Secrets.GithubToken));

        _ = Assert.Single(h.Lines());
        Assert.DoesNotContain(Secrets.Body, h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_secret_in_the_category_or_event_name_is_redacted()
    {
        using var h = new Harness();

        h.Logger("cat " + Secrets.GithubToken).Emit(LogLevel.Information, "x", new EventId(1, "ev " + Secrets.GithubToken));

        _ = Assert.Single(h.Lines());
        Assert.DoesNotContain(Secrets.Body, h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blocked_scrub_withholds_the_text_counts_it_and_still_writes_the_line()
    {
        using var h = new Harness(redactor: new BlockingRedactor());

        h.Logger().Emit(LogLevel.Information, "payload " + BlockingRedactor.Marker + " " + Secrets.Bearer);

        var line = Assert.Single(h.Lines());
        Assert.Equal("[REDACTED:blocked]", line.GetProperty("message").GetString());
        Assert.DoesNotContain(BlockingRedactor.Marker, h.Sink.Text, StringComparison.Ordinal);
        Assert.Equal(1, h.Runtime.Withheld.Logs);
    }

    [Fact]
    public void A_secret_named_hole_is_withheld_while_other_holes_keep_their_format()
    {
        using var h = new Harness();
        var logger = h.Logger();

        Log.LoginWithFormat(logger, 1234.5, "hunter2-not-a-shape");

        var line = Assert.Single(h.Lines());
        Assert.Equal("{took 1,234.50} as [REDACTED:secret-field]", line.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("login {@Password}", "@Password")]
    [InlineData("using {$Token}", "$Token")]
    public void A_destructuring_prefix_does_not_hide_a_secret_name(string template, string key)
    {
        using var h = new Harness();

        h.Logger().EmitTemplate(template, (key, "hunter2-plain"));

        var line = Assert.Single(h.Lines());
        Assert.Equal("[REDACTED:secret-field]", line.GetProperty("fields").GetProperty(key).GetString());
        Assert.DoesNotContain("hunter2", h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_array_field_is_written_as_its_scrubbed_elements()
    {
        using var h = new Harness();

        h.Logger().EmitTemplate("sent {Headers}", ("Headers", new[] { Secrets.Bearer, "plain" }));

        var headers = Assert.Single(h.Lines()).GetProperty("fields").GetProperty("Headers").GetString();
        Assert.NotNull(headers);
        Assert.Contains("[REDACTED:", headers, StringComparison.Ordinal);
        Assert.EndsWith(", plain", headers, StringComparison.Ordinal);
        Assert.DoesNotContain(Secrets.Body, h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_generated_method_with_reordered_parameters_never_moves_a_secret_into_another_hole()
    {
        using var h = new Harness();
        var logger = h.Logger();

        Log.SignIn(logger, "hunter2-plain", "alice");

        var line = Assert.Single(h.Lines());
        Assert.Equal("alice signed in with [REDACTED:secret-field]", line.GetProperty("message").GetString());
        Assert.DoesNotContain("hunter2", h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dictionary_with_secret_named_keys_is_withheld_per_key_in_message_and_fields()
    {
        using var h = new Harness();
        var creds = new Dictionary<string, object?>(StringComparer.Ordinal) { ["password"] = "hunter2-plain", ["user"] = "alice" };

        h.Logger().EmitTemplate("c {Creds}", ("Creds", creds));

        var line = Assert.Single(h.Lines());
        Assert.Equal("c [password, [REDACTED:secret-field]], [user, alice]", line.GetProperty("message").GetString());
        Assert.Equal("[password, [REDACTED:secret-field]], [user, alice]", line.GetProperty("fields").GetProperty("Creds").GetString());
        Assert.DoesNotContain("hunter2", h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_secret_held_as_a_char_array_is_scrubbed_as_one_string()
    {
        using var h = new Harness();

        h.Logger().EmitTemplate("v {V}", ("V", Secrets.GithubToken.ToCharArray()));

        _ = Assert.Single(h.Lines());
        Assert.DoesNotContain("g, h, p", h.Sink.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Secrets.Body, h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_secret_in_a_field_key_is_scrubbed()
    {
        using var h = new Harness();

        h.Logger().EmitTemplate("x", ("x." + Secrets.GithubToken, 1));

        _ = Assert.Single(h.Lines());
        Assert.DoesNotContain(Secrets.Body, h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Key_value_shapes_of_any_value_type_withhold_secret_named_entries()
    {
        using var h = new Harness();
        var ints = new List<KeyValuePair<string, int>> { new("password", 98765), new("n", 1) };
        var tuples = new List<(string, string)> { ("password", "hunter2-plain") };
        var pair = new KeyValuePair<string, string>("password", "hunter2-plain");

        h.Logger().EmitTemplate("{Ints} {Tuples} {Pair}", ("Ints", ints), ("Tuples", tuples), ("Pair", pair));

        var line = Assert.Single(h.Lines());
        Assert.DoesNotContain("98765", h.Sink.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", h.Sink.Text, StringComparison.Ordinal);
        Assert.Contains("[n, 1]", line.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_with_a_secret_named_member_is_withheld_in_its_text()
    {
        using var h = new Harness();

        h.Logger().EmitTemplate("login {Request}", ("Request", new LoginRequest("alice", "hunter2-plain")));

        var line = Assert.Single(h.Lines());
        Assert.Equal("login LoginRequest { User = alice, Password = [REDACTED:secret-field] }", line.GetProperty("message").GetString());
        Assert.DoesNotContain("hunter2", h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hole_name_with_spaces_is_still_recognised_as_a_secret_name()
    {
        using var h = new Harness();

        h.Logger().EmitTemplate("login { Password }", (" Password ", "hunter2-plain"));

        _ = Assert.Single(h.Lines());
        Assert.DoesNotContain("hunter2", h.Sink.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("hunter2-plain")]
    [InlineData("p,ss-hunter2")]
    [InlineData("p}ss-hunter2")]
    [InlineData("a, hunter2 } b")]
    public void A_nested_record_never_shows_any_part_of_a_secret_member(string password)
    {
        using var h = new Harness();
        var session = new Session("s1", new LoginRequest("alice", password));

        h.Logger().EmitTemplate("{Session} {Pair} {Anon}", ("Session", session),
            ("Pair", new KeyValuePair<string, LoginRequest>("req", session.Login)), ("Anon", new { Id = 1, Auth = new { Token = password } }));

        var line = Assert.Single(h.Lines());
        Assert.DoesNotContain("hunter2", h.Sink.Text, StringComparison.Ordinal);
        Assert.StartsWith("Session { Id = s1, Login = LoginRequest { User = alice, Password = [REDACTED:secret-field]", line.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_value_pair_nested_in_a_key_value_pair_withholds_the_inner_secret()
    {
        using var h = new Harness();
        var nested = new List<KeyValuePair<string, KeyValuePair<string, string>>> { new("cfg", new("password", "hunter2-nested")) };

        h.Logger().EmitTemplate("{Outer}", ("Outer", nested));

        _ = Assert.Single(h.Lines());
        Assert.DoesNotContain("hunter2", h.Sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_list_that_contains_itself_is_written_without_overflowing_the_stack()
    {
        using var h = new Harness();
        var list = new List<object>();
        list.Add(list);

        h.Logger().EmitTemplate("{L}", ("L", list));

        Assert.Contains("[REDACTED:too-deep]", Assert.Single(h.Sink.Lines), StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_value_pair_or_tuple_inside_an_object_is_cut_at_its_secret_key()
    {
        using var h = new Harness();
        var anon = new { User = "a", Pair = new KeyValuePair<string, string>("password", "hunter2-plain") };
        var pair = new KeyValuePair<string, (string, string)>("creds", ("password", "hunter2-plain"));

        h.Logger().EmitTemplate("{Anon} {Pair}", ("Anon", anon), ("Pair", pair));

        var line = Assert.Single(h.Lines());
        Assert.Equal("{ User = a, Pair = [password, [REDACTED:secret-field]] } [creds, (password, [REDACTED:secret-field])]", line.GetProperty("message").GetString());
        Assert.DoesNotContain("hunter2", h.Sink.Text, StringComparison.Ordinal);
    }
}
