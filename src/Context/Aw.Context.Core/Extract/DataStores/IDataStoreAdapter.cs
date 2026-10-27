namespace Aw.Context.Core.Extract.DataStores;

/// <summary>
/// Reads one kind of data store out of a repo's files (pure text in, names out): which stores a schema file declares and which
/// of those a code file reads or writes. A language or query-library adapter is a new implementation registered beside the others.
/// </summary>
public interface IDataStoreAdapter
{
    /// <summary>The adapter's name, for notes.</summary>
    public string Name { get; }

    /// <summary>The stores (tables, collections) a schema file declares.</summary>
    /// <param name="file">A file that matches the schema globs.</param>
    /// <returns>The store names as written, without quotes or schema qualifiers.</returns>
    public IEnumerable<string> Declared(RepoFile file);

    /// <summary>The uses of the declared stores a code file shows. A store named only in a comment or a log text is not a use.</summary>
    /// <param name="file">A code file.</param>
    /// <param name="stores">The declared store names, lower case.</param>
    /// <returns>One entry per use found.</returns>
    public IEnumerable<StoreAccess> Accesses(RepoFile file, IReadOnlySet<string> stores);
}
