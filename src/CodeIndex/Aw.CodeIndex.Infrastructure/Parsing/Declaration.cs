using Aw.CodeIndex.Core.Model;
using Microsoft.CodeAnalysis.Text;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>What the walker knows about a declaration before it becomes a draft.</summary>
internal sealed record Declaration
{
    public required string Name { get; init; }

    public required string Qualified { get; init; }

    public required SymbolKind Kind { get; init; }

    public required int Arity { get; init; }

    public string ParameterTypes { get; init; } = string.Empty;

    public required bool Exported { get; init; }

    /// <summary>The span the signature is cut from (after the attributes, up to the end of the head).</summary>
    public required TextSpan Head { get; init; }

    /// <summary>True for a callable (it keeps its body and gets tokens and a complexity), false for a type.</summary>
    public required bool Callable { get; init; }
}
