using Aw.Context.Core.Code;
using Aw.Context.Core.Map;

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
        Data = new CodeData(
            [.. view.Data.Things.Select(t => t with { Label = text.Label(t.Label, t.Trust) })],
            [.. view.Data.Files.Select(f => f with { Path = text.Line(f.Path), Label = text.Line(f.Path), Matched = text.Line(f.Matched) })]),
    };
}
