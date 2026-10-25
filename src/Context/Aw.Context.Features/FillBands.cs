using System.Diagnostics;

namespace Aw.Context.Features;

/// <summary>
/// The spans of the context fills' bands (issue 1101), named 'Aw.Context' under the default 'Aw.*' listen pattern: 'context.fill.lenses' (the lenses walk), 'context.fill.code.lexical', 'context.fill.code.symbols'
/// and 'context.fill.history'. A span's duration is the band's time, so concurrent fills show which band is slow. Without a listener a span is not created; nothing reaches an answer.
/// </summary>
internal static class FillBands
{
    internal const string Lenses = "context.fill.lenses";
    internal const string CodeLexical = "context.fill.code.lexical";
    internal const string CodeSymbols = "context.fill.code.symbols";
    internal const string History = "context.fill.history";

    internal static ActivitySource Source { get; } = new("Aw.Context");

    /// <summary>Starts the span of one band; dispose it when the band is done.</summary>
    internal static Activity? Start(string band) => Source.StartActivity(band);
}
