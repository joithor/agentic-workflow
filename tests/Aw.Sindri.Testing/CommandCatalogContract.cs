using Aw.Sindri.Core.Commands;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="ICommandCatalog"/> honours: one row per action id, For(type) returns exactly that type's
/// rows, and every row's CLI form is checked. Run against the fake and the Host's catalog.
/// </summary>
public abstract class CommandCatalogContract
{
    /// <summary>The catalog under test (with at least one row bound to a command type).</summary>
    /// <returns>The catalog.</returns>
    protected abstract ICommandCatalog Create();

    /// <summary>No action id appears twice.</summary>
    [Fact]
    public void Action_ids_are_unique()
    {
        var rows = Create().Rows;

        Expect.Equal(rows.Count, rows.Select(row => row.Action.Value).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>For(type) returns exactly the rows bound to that type, in catalog order.</summary>
    [Fact]
    public void For_returns_exactly_the_rows_of_that_type()
    {
        var catalog = Create();
        var types = catalog.Rows.Select(row => row.CommandType).OfType<Type>().Distinct().ToList();

        Expect.True(types.Count > 0);
        foreach (var type in types)
        {
            Expect.Sequence(catalog.Rows.Where(row => row.CommandType == type), catalog.For(type));
        }

        Expect.True(catalog.For(typeof(CommandCatalogContract)).Count == 0);
    }

    /// <summary>Every row's CLI form reads back through CliForm.Parse.</summary>
    [Fact]
    public void Every_cli_form_parses()
    {
        foreach (var row in Create().Rows)
        {
            Expect.True(CliForm.Parse(row.Cli.Text).IsSuccess);
        }
    }
}
