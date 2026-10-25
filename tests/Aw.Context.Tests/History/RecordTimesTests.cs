using Aw.Context.Core.History;

namespace Aw.Context.Tests.History;

public sealed class RecordTimesTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_timestamp_version_is_the_update_time_and_anything_else_is_unknown()
    {
        var created = new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(new RecordTimes(created, new DateTimeOffset(2026, 7, 23, 9, 30, 0, TimeSpan.Zero)), RecordTimes.Of(created, "2026-07-23T09:30:00Z"));
        Assert.Equal(new RecordTimes(CreatedAt: null, UpdatedAt: null), RecordTimes.Of(createdAt: null, "v17"));
        Assert.Equal(new RecordTimes(created, UpdatedAt: null), RecordTimes.Of(created, "2026-07-23 not a time"));
    }
}
