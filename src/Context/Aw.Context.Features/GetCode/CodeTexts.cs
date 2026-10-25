using Aw.Context.Core.Code;
using Aw.Context.Core.Map;
using Aw.Context.Core.Symbols;

namespace Aw.Context.Features.GetCode;

/// <summary>Cleans every piece of map text in a code view the way every answer's is cleaned (spec §7): scrubbed, one line for a label, fenced when the caller's policy fences untrusted text. File names are never fenced.</summary>
internal static class CodeTexts
{
    internal static CodeView Clean(CodeView view, MapText text) => view with
    {
        Anchors = [.. view.Anchors.Select(a => a with { Label = text.Label(a.Label, a.Trust) })],
        MissingAnchors = [.. view.MissingAnchors.Select(text.Line)],
        Files = [.. view.Files.Select(f => f with { Path = text.Line(f.Path), Label = text.Line(f.Path), TestedBy = [.. f.TestedBy.Select(text.Line)] })],
        Tests = [.. view.Tests.Select(t => t with { Path = text.Line(t.Path), Label = text.Line(t.Path), Named = [.. t.Named.Select(text.Line)] })],
        Callers = Symbols(view.Callers, text),
        Callees = Symbols(view.Callees, text),
        Data = new CodeData(
            [.. view.Data.Things.Select(t => t with { Label = text.Label(t.Label, t.Trust) })],
            [.. view.Data.Files.Select(f => f with { Path = text.Line(f.Path), Label = text.Line(f.Path), Matched = text.Line(f.Matched) })]),
    };

    // Names, signatures and paths of the index are repo text: one clean line each, like the paths above.
    private static CodeSymbols Symbols(CodeSymbols symbols, MapText text) =>
        symbols with { Items = [.. symbols.Items.Select(e => e with { Symbol = Site(e.Symbol, text) })] };

    internal static SymbolSite Site(SymbolSite site, MapText text) => site with
    {
        Ref = OneLine(site.Ref, text),
        Name = OneLine(site.Name, text),
        Kind = OneLine(site.Kind, text),
        Path = OneLine(site.Path, text),
        Signature = site.Signature is null ? null : OneLine(site.Signature, text),
    };

    // Line breaks and runs of white space become one space before the scrub, so a planted line break cannot start a new line of an answer.
    private static string OneLine(string value, MapText text) => text.Line(string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
}
