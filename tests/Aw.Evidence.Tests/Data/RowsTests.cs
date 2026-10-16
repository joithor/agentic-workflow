using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class RowsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Rows_write_a_json_array_of_objects_keyed_by_column()
    {
        var rows = new Rows(["id", "text"], [[1L, "a"], [2L, "b"]], Truncated: false);

        Assert.Equal("""[{"id":1,"text":"a"},{"id":2,"text":"b"}]""", rows.ToJson());
    }

    [Fact, Trait("Lane", "unit")]
    public void Rows_write_each_kind_of_value_as_one_json_value()
    {
        object?[] values = [null, DBNull.Value, true, 7, 8L, 1.5d, 2.25m, "t\"q", new byte[] { 0xCA, 0xFE }, new DateTime(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc), Guid.Empty, new[] { 1, 2 }, double.NaN];
        var rows = new Rows([.. values.Select((_, i) => "c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))], [values], Truncated: false);

        var json = rows.ToJson();

        Assert.Equal(
            """[{"c0":null,"c1":null,"c2":true,"c3":7,"c4":8,"c5":1.5,"c6":2.25,"c7":"t"q","c8":"\\xcafe","c9":"2026-10-15T09:00:00.0000000Z","c10":"00000000-0000-0000-0000-000000000000","c11":[1,2],"c12":"NaN"}]""",
            json);
    }

    [Fact, Trait("Lane", "unit")]
    public void Rows_with_no_rows_are_an_empty_array()
        => Assert.Equal("[]", new Rows(["id"], [], Truncated: false).ToJson());
}
