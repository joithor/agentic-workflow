using Aw.CodeIndex.Core.Model;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// One language's parser (spec §5.3), chosen by file extension through a table in Core. An implementation must read text
/// only (no project loading, no analyzer or generator of the repo), stay inside the depth, time and size guards, report
/// a file it gave up on as a <see cref="ParseOutcome.Skip"/> and never throw for hostile input.
/// </summary>
public interface ISymbolParser
{
    /// <summary>The parser's version word ('csharp-1'); it is part of the structure stamp, so a new version parses every file again.</summary>
    public string Version { get; }

    /// <summary>Whether this parser reads the file.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>True for the extensions the parser covers.</returns>
    public bool Handles(string path);

    /// <summary>Parses one file's text.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="text">The file's text.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The symbols or the skip reason.</returns>
    public Result<ParseOutcome> Parse(string path, string text, CancellationToken cancellationToken);
}
