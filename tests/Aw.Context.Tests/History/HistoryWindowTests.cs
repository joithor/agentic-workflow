using Aw.Context.Core;
using Aw.Context.Core.History;

namespace Aw.Context.Tests.History;

public sealed class HistoryWindowTests
{
    [Fact, Trait("Lane", "unit")]
    public void Window_parses_instant_offset_and_date_and_refuses_everything_else_006()
    {
        var window = HistoryWindow.Of("2025-09-01T00:00:00Z", "2025-10-01T02:00:00+02:00").Value;

        Assert.Equal(new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero), window.Since);
        Assert.Equal(new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero), window.Before!.Value.ToUniversalTime());
        Assert.Equal(new DateTimeOffset(2025, 3, 10, 0, 0, 0, TimeSpan.Zero), HistoryWindow.Of(since: null, "2025-03-10").Value.Before);
        Assert.Equal(HistoryWindow.Open, HistoryWindow.Of(since: null, before: null).Value);
        foreach (var bad in new[] { "90d", "yesterday", "2025-09-01T00:00:00", "2025-13-01", "", " ", "2025-09-01 00:00:00Z" })
        {
            Assert.Equal(ContextErrors.QueryInvalid.Code, HistoryWindow.Of(bad, before: null).Error.Code);
            Assert.Equal(ContextErrors.QueryInvalid.Code, HistoryWindow.Of(since: null, bad).Error.Code);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Since_after_before_is_006() =>
        Assert.Equal(ContextErrors.QueryInvalid.Code, HistoryWindow.Of("2025-09-02", "2025-09-01").Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void Before_is_inclusive()
    {
        var window = HistoryWindow.Of(since: null, "2025-09-22T11:00:00Z").Value;
        var instant = new DateTimeOffset(2025, 9, 22, 11, 0, 0, TimeSpan.Zero);

        Assert.True(window.NotAfterCut(instant));
        Assert.True(window.NotAfterCut(instant.AddSeconds(-1)));
        Assert.False(window.NotAfterCut(instant.AddSeconds(1)));
        Assert.True(HistoryWindow.Open.NotAfterCut(instant));
    }
}
