using System.Collections.Concurrent;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The native Linear adapter's GraphQL operations (D18): each a '.graphql' file embedded in this assembly, named 'linear.&lt;Operation&gt;.graphql', read once.</summary>
internal static class LinearOperations
{
    private static readonly ConcurrentDictionary<string, string> _texts = new(StringComparer.Ordinal);

    /// <summary>The text of an operation.</summary>
    /// <param name="name">The file's name without its extension ('Projects', 'Issue', ...).</param>
    /// <returns>The GraphQL text.</returns>
    /// <exception cref="InvalidOperationException">No such operation is embedded (a bug in the adapter).</exception>
    public static string Text(string name) => _texts.GetOrAdd(name, static n =>
    {
        using var stream = typeof(LinearOperations).Assembly.GetManifestResourceStream($"linear.{n}.graphql")
            ?? throw new InvalidOperationException($"The linear operation '{n}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
