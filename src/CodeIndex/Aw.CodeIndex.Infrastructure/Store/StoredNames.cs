using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>The text each enum is stored as. Reading an unknown text is a damaged file, never a guess.</summary>
internal static class StoredNames
{
    internal static string Text(LayerName value) => value switch
    {
        LayerName.Structure => "structure",
        LayerName.Clones => "clones",
        LayerName.Deps => "deps",
        LayerName.Graph => "graph",
        LayerName.Embeddings => "embeddings",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static string Text(LayerState value) => value switch
    {
        LayerState.Ok => "ok",
        LayerState.Unavailable => "unavailable",
        LayerState.Disabled => "disabled",
        LayerState.Pending => "pending",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static string Text(EdgeKind value) => value switch
    {
        EdgeKind.Calls => "calls",
        EdgeKind.Inherits => "inherits",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static string Text(EdgeBasis value) => value switch
    {
        EdgeBasis.Syntax => "syntax",
        EdgeBasis.Graphify => "graphify",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static string Text(Confidence value) => value switch
    {
        Confidence.Certain => "certain",
        Confidence.Likely => "likely",
        Confidence.Ambiguous => "ambiguous",
        Confidence.Unresolved => "unresolved",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static string Text(SymbolKind value) => value switch
    {
        SymbolKind.Function => "function",
        SymbolKind.Method => "method",
        SymbolKind.Constructor => "constructor",
        SymbolKind.Accessor => "accessor",
        SymbolKind.LocalFunction => "local-function",
        SymbolKind.Lambda => "lambda",
        SymbolKind.Class => "class",
        SymbolKind.Record => "record",
        SymbolKind.Struct => "struct",
        SymbolKind.Interface => "interface",
        SymbolKind.Enum => "enum",
        SymbolKind.GraphSymbol => "graph-symbol",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static LayerName LayerOf(string text) => Parse<LayerName>(text, Text);

    internal static LayerState StateOf(string text) => Parse<LayerState>(text, Text);

    internal static EdgeKind KindOf(string text) => Parse<EdgeKind>(text, Text);

    internal static EdgeBasis BasisOf(string text) => Parse<EdgeBasis>(text, Text);

    internal static Confidence ConfidenceOf(string text) => Parse<Confidence>(text, Text);

    internal static SymbolKind SymbolKindOf(string text) => Parse<SymbolKind>(text, Text);

    private static T Parse<T>(string text, Func<T, string> name)
        where T : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<T>())
        {
            if (string.Equals(name(candidate), text, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        throw new InvalidDataException($"'{text}' is not a stored {typeof(T).Name}.");
    }
}
