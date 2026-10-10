using Aw.Kernel.Services;

namespace Aw.Kernel.Tests;

[Trait("Lane", "unit")]
public sealed class DecoratedServiceTests
{
    [Fact]
    public async Task A_valid_request_reaches_the_inner_service()
    {
        var inner = new Doubler();
        var service = new ValidatingService<int, int>(inner, new PositiveOnly());

        var result = await service.Handle(21, TestContext.Current.CancellationToken);

        Assert.Equal(42, result.Value);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task An_invalid_request_is_an_error_and_never_reaches_the_inner_service()
    {
        var inner = new Doubler();
        var service = new ValidatingService<int, int>(inner, new PositiveOnly());

        var result = await service.Handle(-1, TestContext.Current.CancellationToken);

        Assert.Equal("AW-KERNEL-001", result.Error.Code);
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task Decorators_compose_outermost_first()
    {
        var log = new List<string>();
        IService<int, int> service = new Recording(new Recording(new Doubler(), log, "inner"), log, "outer");

        _ = await service.Handle(1, TestContext.Current.CancellationToken);

        Assert.Equal(["outer>", "inner>", "<inner", "<outer"], log);
    }

    private sealed class Doubler : IService<int, int>
    {
        public int Calls { get; private set; }

        public ValueTask<Result<int>> Handle(int request, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult<Result<int>>(request * 2);
        }
    }

    private sealed class PositiveOnly : IValidator<int>
    {
        public Result<Unit> Validate(int value) =>
            value > 0 ? Results.Ok() : KernelErrors.Invalid.With(value.ToString(System.Globalization.CultureInfo.InvariantCulture) + " is not positive");
    }

    private sealed class Recording(IService<int, int> inner, List<string> log, string name) : Decorated<int, int>(inner)
    {
        public override async ValueTask<Result<int>> Handle(int request, CancellationToken cancellationToken)
        {
            log.Add(name + ">");
            var result = await Inner.Handle(request, cancellationToken).ConfigureAwait(false);
            log.Add("<" + name);
            return result;
        }
    }
}
