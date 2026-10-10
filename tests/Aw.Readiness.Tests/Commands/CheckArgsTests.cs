using Aw.Readiness.App.Commands;

namespace Aw.Readiness.Tests.Commands;

public sealed class CheckArgsTests
{
    [Fact, Trait("Lane", "unit")]
    public void No_flags_checks_the_current_directory_with_the_host_as_text()
    {
        var args = CheckArgs.Parse([]);

        Assert.Equal(new CheckArgs(".", UseHost: true, Json: false), args.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Flags_set_the_root_json_and_offline()
    {
        var args = CheckArgs.Parse(["--json", "--root", "/r", "--no-remote"]);

        Assert.Equal(new CheckArgs("/r", UseHost: false, Json: true), args.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--bogus")]
    [InlineData("--root")]
    [InlineData("--json", "--json")]
    [InlineData("extra")]
    public void Unknown_repeated_or_incomplete_flags_are_refused(params string[] argv)
    {
        var args = CheckArgs.Parse(argv);

        Assert.True(args.IsError);
        Assert.Equal("AW-READINESS-001", args.Error.Code);
        Assert.Contains("usage: aw-readiness check", args.Error.Message, StringComparison.Ordinal);
    }
}
