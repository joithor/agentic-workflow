namespace Aw.Kernel.Tests;

[Trait("Lane", "unit")]
public sealed class ResultAsyncTests
{
    private static readonly Error _invalid = KernelErrors.Invalid.With("odd");

    [Fact]
    public async Task Map_and_bind_chain_over_a_service_result()
    {
        var result = await Handle(20)
            .Map(v => v + 1)
            .BindAsync(Half)
            .MapAsync(v => ValueTask.FromResult(v * 10));

        Assert.Equal(_invalid, result.Error);
        Assert.Equal(100, (await Handle(20).BindAsync(Half).MapAsync(v => ValueTask.FromResult(v * 10))).Value);
    }

    [Fact]
    public async Task Async_steps_run_only_on_success()
    {
        var calls = 0;
        Result<int> failed = _invalid;

        var mapped = await failed.MapAsync(v => { calls++; return ValueTask.FromResult(v); });
        var bound = await failed.BindAsync(v => { calls++; return ValueTask.FromResult<Result<int>>(v); });
        var chained = await Handle(1).Bind(_ => Results.Failure<int>(_invalid)).Bind(v => { calls++; return Results.Success(v); });

        Assert.Equal((0, _invalid, _invalid, _invalid), (calls, mapped.Error, bound.Error, chained.Error));
    }

    private static ValueTask<Result<int>> Handle(int value) => ValueTask.FromResult<Result<int>>(value);

    private static ValueTask<Result<int>> Half(int value) =>
        ValueTask.FromResult(value % 2 == 0 ? Results.Success(value / 2) : Results.Failure<int>(_invalid));
}
