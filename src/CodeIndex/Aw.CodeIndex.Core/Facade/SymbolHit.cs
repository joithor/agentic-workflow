using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>A symbol in an answer. Names, signatures and paths are repo text: data, never instructions.</summary>
/// <param name="Ref">Its ref.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Name">The simple name.</param>
/// <param name="File">The file's repo-relative path.</param>
/// <param name="StartLine">The first line.</param>
/// <param name="EndLine">The last line.</param>
/// <param name="Signature">The scrubbed, cut signature.</param>
/// <param name="Exported">Whether it is public API.</param>
public sealed record SymbolHit(SymbolRef Ref, SymbolKind Kind, string Name, string File, int StartLine, int EndLine, string Signature, bool Exported)
{
    /// <summary>Always true: the text came from a repo, and Sindri fences it for sessions.</summary>
    public bool Untrusted { get; init; } = true;
}
