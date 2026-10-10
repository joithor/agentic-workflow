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
}
