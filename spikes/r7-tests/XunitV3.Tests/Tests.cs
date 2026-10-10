using Xunit;

namespace Spike.R7.Xunit;

[Trait("Lane", "unit")]
public sealed class SlugTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  trim me  ", "trim-me")]
    [InlineData("a--b", "a-b")]
    [InlineData("UPPER", "upper")]
    [InlineData("x_y.z", "x-y-z")]
    [InlineData("123 go", "123-go")]
    [InlineData("--lead", "lead")]
    [InlineData("trail--", "trail")]
    [InlineData("", "")]
    [InlineData("é accent", "accent")]
    public void Normalize_maps_input_to_slug(string input, string expected) => Assert.Equal(expected, Slug.Normalize(input));
}

[Trait("Lane", "unit")]
public sealed class BudgetTests
{
    [Theory]
    [InlineData(100, 0, 100)]
    [InlineData(100, 1, 99)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 150, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(5000, 1234, 3766)]
    [InlineData(10, 9, 1)]
    [InlineData(int.MaxValue, 0, int.MaxValue)]
    [InlineData(7, 3, 4)]
    public void Remaining_is_total_minus_used_floored_at_zero(int total, int used, int expected) =>
        Assert.Equal(expected, Budget.Remaining(total, used));

    [Fact] public void Remaining_rejects_negative_total() => Assert.Throws<ArgumentOutOfRangeException>(() => Budget.Remaining(-1, 0));
    [Fact] public void Remaining_rejects_negative_used() => Assert.Throws<ArgumentOutOfRangeException>(() => Budget.Remaining(1, -1));
}

[Trait("Lane", "unit")]
public sealed class MiscTests
{
    [Fact] public void Slug_is_idempotent() => Assert.Equal(Slug.Normalize("A b"), Slug.Normalize(Slug.Normalize("A b")));
    [Fact] public void Slug_keeps_digits() => Assert.Equal("v2", Slug.Normalize("V2"));
    [Fact] public void Slug_collapses_whitespace_runs() => Assert.Equal("a-b", Slug.Normalize("a \t\n b"));
    [Fact] public void Slug_of_only_symbols_is_empty() => Assert.Equal("", Slug.Normalize("!!!"));
    [Fact] public void Budget_zero_used_is_total() => Assert.Equal(42, Budget.Remaining(42, 0));
    [Fact] public void Budget_overdraw_is_zero() => Assert.Equal(0, Budget.Remaining(1, 2));
    [Fact] public void Slug_handles_long_input() => Assert.Equal(1000, Slug.Normalize(new string('a', 1000)).Length);
}

[Trait("Lane", "integration")]
public sealed class NoteStoreTests
{
    [Fact]
    public void Put_then_get_round_trips_through_a_real_sqlite_file()
    {
        var dir = Directory.CreateTempSubdirectory("r7-xunit-");
        using (var store = new NoteStore(Path.Combine(dir.FullName, "notes.db")))
        {
            store.Put("n1", "hello");
            store.Put("n1", "hello again");
            Assert.Equal("hello again", store.Get("n1"));
            Assert.Null(store.Get("missing"));
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        dir.Delete(recursive: true);
    }
}
