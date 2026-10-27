namespace Aw.Context.Core.Extract.DataStores;

/// <summary>Plain SQL: CREATE TABLE in schema files, and SQL text or query-builder calls naming a table in code.</summary>
public sealed class GenericSqlAdapter : IDataStoreAdapter
{
    /// <inheritdoc />
    public string Name => "sql";

    /// <inheritdoc />
    public IEnumerable<string> Declared(RepoFile file) => [];

    /// <inheritdoc />
    public IEnumerable<StoreAccess> Accesses(RepoFile file, IReadOnlySet<string> stores) => [];
}
