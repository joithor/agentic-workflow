using Aw.CodeIndex.Core.Model;
using Microsoft.CodeAnalysis;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>A call, creation or inheritance as the syntax shows it; <see cref="At"/> gives its line.</summary>
internal sealed record RawSite
{
    public required EdgeKind Kind { get; init; }

    public required string Name { get; init; }

    public required int Arity { get; init; }

    public required string Receiver { get; init; }

    public required SyntaxToken At { get; init; }
}
