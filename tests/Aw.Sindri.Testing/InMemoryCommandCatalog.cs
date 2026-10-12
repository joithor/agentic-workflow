using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory command catalog: the rows a test gives it; kept honest by CommandCatalogContract.</summary>
/// <param name="rows">The rows.</param>
public sealed class InMemoryCommandCatalog(params CommandRow[] rows) : ICommandCatalog
{
    /// <inheritdoc/>
    public IReadOnlyList<CommandRow> Rows { get; } = rows;

    /// <inheritdoc/>
    public IReadOnlyList<CommandRow> For(Type commandType) => [.. Rows.Where(row => row.CommandType == commandType)];
}
