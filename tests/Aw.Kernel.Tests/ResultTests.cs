using Aw.Kernel;

namespace Aw.Kernel.Tests;

[Trait("Lane", "unit")]
public sealed class ResultTests
{
    private static readonly Error NotFound = KernelErrors.NotFound.With("item 42 is not in the backlog");

    [Fact]
    public void Success_carries_the_value_and_no_error()
    {
        Result<int> result = 7;

        Assert.True(result.IsSuccess);
        Assert.False(result.IsError);
        Assert.Equal(7, result.Value);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_carries_the_error_and_reading_the_value_is_a_bug()
    {
        Result<int> result = NotFound;

        Assert.True(result.IsError);
        Assert.Same(NotFound, result.Error);
        var bug = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("AW-KERNEL-002", bug.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Map_and_bind_run_only_on_success()
    {
        Result<int> ok = 20;
        Result<int> failed = NotFound;

        Assert.Equal("21", ok.Map(v => v + 1).Map(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)).Value);
        Assert.Equal(10, ok.Bind(v => Half(v)).Value);
        Assert.Same(NotFound, failed.Map(v => v + 1).Error);
        Assert.Same(NotFound, failed.Bind(v => Half(v)).Error);
        Assert.Equal(ErrorKind.Invalid, ok.Bind(_ => Half(3)).Error.Kind);
    }

    [Fact]
    public void Match_picks_the_branch()
    {
        Result<int> ok = 1;
        Result<int> failed = NotFound;

        Assert.Equal("value 1", ok.Match(v => $"value {v}", e => e.Code));
        Assert.Equal("AW-KERNEL-002", failed.Match(v => $"value {v}", e => e.Code));
    }

    [Fact]
    public void Ok_is_a_unit_success_and_named_factories_match_the_conversions()
    {
        Assert.True(Result.Ok().IsSuccess);
        Assert.Equal(Unit.Value, Result.Ok().Value);
        Assert.Equal(5, Result.Success(5).Value);
        Assert.Same(NotFound, Result.Failure<int>(NotFound).Error);
    }

    private static Result<int> Half(int value) =>
        value % 2 == 0 ? value / 2 : KernelErrors.Invalid.With($"{value} is odd");
}
