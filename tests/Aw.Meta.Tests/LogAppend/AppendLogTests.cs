using Aw.Meta.Core;
using Aw.Meta.Features.LogAppend;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LogAppend;

public sealed class AppendLogTests
{
    private readonly MetaWorld _world = new();

    private AppendLogService Service() => new(_world.Log, _world.Clock);

    [Fact, Trait("Lane", "unit")]
    public async Task A_line_is_appended_with_the_kind_and_the_time()
    {
        var response = await Service().Handle(new AppendLogRequest("decision", "gate before merge"), CancellationToken.None);

        Assert.Equal("- DECISION 2026-10-15 05:00: gate before merge", response.Value.Line);
        Assert.Equal([response.Value.Line], _world.Log.Lines);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("shout", "x")]
    [InlineData("note", " ")]
    [InlineData("", "x")]
    public void An_unknown_kind_or_blank_text_is_refused(string kind, string text) =>
        Assert.Equal(MetaErrors.Invalid.Code, new AppendLogValidator().Validate(new AppendLogRequest(kind, text)).Error.Code);
}
