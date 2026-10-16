using Aw.Meta.Core.Logs;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Logs;

public sealed class LogLineTests
{
    [Fact, Trait("Lane", "unit")]
    public void An_event_line_has_the_kind_the_time_and_one_line_of_text() =>
        Assert.Equal("- MERGE 2026-10-15 04:05: #190 merged", LogLine.Event(LogKind.Merge, A.At(5), "#190 merged\r\n"));

    [Fact, Trait("Lane", "unit")]
    public void The_owner_line_names_the_date_and_who_asked() =>
        Assert.Equal("- OWNER (2026-10-15): use sqlite (asked by t1-api)", LogLine.Owner(A.At(5), "use sqlite", "t1-api"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("merge", true)]
    [InlineData("INCIDENT", true)]
    [InlineData("Note", true)]
    [InlineData("owner", false)]
    [InlineData("3", false)]
    [InlineData("", false)]
    public void Kinds_are_read_in_any_case_and_numbers_are_not_kinds(string text, bool known) =>
        Assert.Equal(known, LogLine.TryParseKind(text, out _));
}
