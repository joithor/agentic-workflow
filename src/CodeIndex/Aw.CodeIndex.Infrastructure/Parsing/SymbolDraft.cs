using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>A symbol while its file is still being walked: its call sites grow until the walk ends.</summary>
internal sealed class SymbolDraft
{
    public required string Name { get; init; }

    public required string Qualified { get; init; }

    public required SymbolKind Kind { get; init; }

    public required int Arity { get; init; }

    public required string ParameterTypesText { get; init; }

    public required int StartLine { get; init; }

    public required int EndLine { get; init; }

    public required bool Exported { get; init; }

    public required string Signature { get; set; }

    public required SyntaxMeasure Measure { get; init; }

    public required string? Body { get; init; }

    public List<CallSite> CallSites { get; } = [];

    public ParsedSymbol ToSymbol() =>
        new(Name, Qualified, Kind, Arity, ParameterTypesText, StartLine, EndLine, Exported, Signature, Measure.ShapeHash, Measure.Complexity, Measure.Tokens, Body, CallSites);
}
