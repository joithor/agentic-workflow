using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class SqlTextTests
{
    [Fact, Trait("Lane", "unit")]
    public void SqlText_splits_at_top_level_semicolons_only()
    {
        var statements = SqlText.Statements("SELECT ';'; SELECT \"a;b\" /* ; */ FROM t -- ;\n; SELECT $tag$ ; $tag$; ;");

        Assert.Equal(3, statements.Count);
        Assert.Equal("SELECT ';'", statements[0]);
        Assert.StartsWith("SELECT \"a;b\"", statements[1], StringComparison.Ordinal);
        Assert.Equal("SELECT $tag$ ; $tag$", statements[2]);
    }

    [Fact, Trait("Lane", "unit")]
    public void SqlText_reads_words_outside_quotes_and_comments()
    {
        var words = SqlText.Words("select 'insert' as x, \"delete\" /* update */ from t -- drop\n where a_1 = 2");

        Assert.Equal(["SELECT", "AS", "X", "FROM", "T", "WHERE", "A_1"], [.. words.Take(7)]);
        Assert.DoesNotContain("INSERT", words, StringComparer.Ordinal);
        Assert.DoesNotContain("DROP", words, StringComparer.Ordinal);
    }
}
