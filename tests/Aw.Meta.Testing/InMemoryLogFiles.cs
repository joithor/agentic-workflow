using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ILogFiles: the one fake, kept honest by LogFilesContract.</summary>
public sealed class InMemoryLogFiles : ILogFiles
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <summary>Puts a file in place.</summary>
    /// <param name="path">The path.</param>
    /// <param name="text">Its text.</param>
    public void Put(string path, string text) => _files[path] = text;

    /// <inheritdoc />
    public ValueTask<Result<string>> Tail(string path, int maxCharacters, CancellationToken cancellationToken) =>
        _files.TryGetValue(path, out var text)
            ? Done.Of(text.Length <= maxCharacters ? text : text[^maxCharacters..])
            : Done.Failed<string>(MetaErrors.LogUnreadable.With("no such file: " + path));
}
