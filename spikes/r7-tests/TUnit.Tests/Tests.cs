using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Spike.R7.TUnitTests;

[Property("Lane", "unit")]
public sealed class SlugTests
{
        [Arguments("Hello World", "hello-world")]
    [Arguments("  trim me  ", "trim-me")]
    [Arguments("a--b", "a-b")]
    [Arguments("UPPER", "upper")]
    [Arguments("x_y.z", "x-y-z")]
    [Arguments("123 go", "123-go")]
    [Arguments("--lead", "lead")]
    [Arguments("trail--", "trail")]
    [Arguments("", "")]
    [Arguments("é accent", "accent")]
    [Test]
    public async Task Normalize_maps_input_to_slug(string input, string expected) => await Assert.That(Slug.Normalize(input)).IsEqualTo(expected);
}

[Property("Lane", "unit")]
public sealed class BudgetTests
{
        [Arguments(100, 0, 100)]
    [Arguments(100, 1, 99)]
    [Arguments(100, 100, 0)]
    [Arguments(100, 150, 0)]
    [Arguments(0, 0, 0)]
    [Arguments(1, 0, 1)]
    [Arguments(5000, 1234, 3766)]
    [Arguments(10, 9, 1)]
    [Arguments(int.MaxValue, 0, int.MaxValue)]
    [Arguments(7, 3, 4)]
    [Test]
    public async Task Remaining_is_total_minus_used_floored_at_zero(int total, int used, int expected) =>
        await Assert.That(Budget.Remaining(total, used)).IsEqualTo(expected);

    [Test] public async Task Remaining_rejects_negative_total() => await Assert.That(() => Budget.Remaining(-1, 0)).Throws<ArgumentOutOfRangeException>();
    [Test] public async Task Remaining_rejects_negative_used() => await Assert.That(() => Budget.Remaining(1, -1)).Throws<ArgumentOutOfRangeException>();
}

[Property("Lane", "unit")]
public sealed class MiscTests
{
    [Test] public async Task Slug_is_idempotent() => await Assert.That(Slug.Normalize(Slug.Normalize("A b"))).IsEqualTo(Slug.Normalize("A b"));
    [Test] public async Task Slug_keeps_digits() => await Assert.That(Slug.Normalize("V2")).IsEqualTo("v2");
    [Test] public async Task Slug_collapses_whitespace_runs() => await Assert.That(Slug.Normalize("a \t\n b")).IsEqualTo("a-b");
    [Test] public async Task Slug_of_only_symbols_is_empty() => await Assert.That(Slug.Normalize("!!!")).IsEqualTo("");
    [Test] public async Task Budget_zero_used_is_total() => await Assert.That(Budget.Remaining(42, 0)).IsEqualTo(42);
    [Test] public async Task Budget_overdraw_is_zero() => await Assert.That(Budget.Remaining(1, 2)).IsEqualTo(0);
    [Test] public async Task Slug_handles_long_input() => await Assert.That(Slug.Normalize(new string('a', 1000)).Length).IsEqualTo(1000);
}

[Property("Lane", "integration")]
public sealed class NoteStoreTests
{
    [Test]
    public async Task Put_then_get_round_trips_through_a_real_sqlite_file()
    {
        var dir = Directory.CreateTempSubdirectory("r7-tunit-");
        using (var store = new NoteStore(Path.Combine(dir.FullName, "notes.db")))
        {
            store.Put("n1", "hello");
            store.Put("n1", "hello again");
            await Assert.That(store.Get("n1")).IsEqualTo("hello again");
            await Assert.That(store.Get("missing")).IsNull();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        dir.Delete(recursive: true);
    }
}
