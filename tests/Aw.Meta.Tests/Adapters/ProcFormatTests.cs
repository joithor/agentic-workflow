using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Tests.Adapters;

public sealed class ProcFormatTests
{
    private const string Tail = "S 1 4242 4242 0 -1 4194560 100 0 0 0 1 2 0 0 20 0 3 0 777 1000000 2500 18446744073709551615";

    [Fact, Trait("Lane", "unit")]
    public void A_stat_line_is_read_even_when_the_name_has_spaces_and_parentheses()
    {
        var fields = ProcFormat.Stat("4242 (my (odd) name) " + Tail);

        Assert.NotNull(fields);
        Assert.Equal(("my (odd) name", 'S', 1, 777L, 2500L), (fields.Name, fields.State, fields.Ppid, fields.StartTicks, fields.RssPages));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("4242 no parens S 1")]
    [InlineData("4242 (short) S 1 2")]
    public void A_stat_line_that_is_not_one_is_none(string stat) => Assert.Null(ProcFormat.Stat(stat));

    [Fact, Trait("Lane", "unit")]
    public void Meminfo_gives_available_memory_and_swap_in_use()
    {
        var fields = ProcFormat.MemInfo("MemTotal:       16000000 kB\nMemAvailable:    9000000 kB\nSwapTotal:       2000000 kB\nSwapFree:        1500000 kB\n");

        Assert.Equal((9_000_000L, 500_000L, 16_000_000L, 2_000_000L), (fields.Available, fields.SwapUsed, fields.Total, fields.SwapTotal));
    }
}
