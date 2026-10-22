using Aw.Context.Core.Code;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Symbols;

namespace Aw.Context.Tests.Code;

public sealed class CodeLensSymbolsTests
{
    private static CodeView Half() => CodeLensBuilder.Build(CodeGraphs.Basis, CodeGraphs.Sharing(), ["feature:sharing"]).Value;

    private static CodeSymbols Ok(string commit) =>
        new(SymbolsState.Ok, Reason: null, [new CallEdgeRow(CodeSymbolSourceContract.Symbols[0], Basis: "semantic", Confidence: 1.0, Coverage: "full", Depth: 1, Commit: commit, AsOfCommit: null, State: SymbolsState.Ok)]) { Commit = commit, Unresolved = 1 };

    [Fact, Trait("Lane", "unit")]
    public void The_findings_fill_callers_callees_and_replace_only_the_code_index_row()
    {
        var view = Half();
        var row = new SearchedSource(SourceId.From("code-index"), SearchedState.Ok, Reason: null, Code: null, new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));

        var merged = CodeLensSymbols.With(view, new CodeSymbolFindings(row, Ok(CodeSymbolSourceContract.Indexed), CodeSymbols.Unavailable));

        var expected = Ok(CodeSymbolSourceContract.Indexed);
        Assert.Equal(expected.Items, merged.Callers.Items);
        Assert.Equal(expected with { Items = [] }, merged.Callers with { Items = [] });
        Assert.Equal(CodeSymbols.Unavailable, merged.Callees);
        Assert.Equal(view.Searched.Sources.Select(s => s.Id.Value), merged.Searched.Sources.Select(s => s.Id.Value), StringComparer.Ordinal);
        Assert.Equal(row, merged.Searched.Sources.Single(s => s.Id.Value == "code-index"));
        Assert.Equal(view.Searched.Sources.Where(s => s.Id.Value != "code-index"), merged.Searched.Sources.Where(s => s.Id.Value != "code-index"));
        Assert.Equal(view.Files, merged.Files);
        Assert.Equal(view.Degraded, merged.Degraded);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_degraded_row_is_listed_in_degraded_with_its_code_or_reason()
    {
        var stale = new SearchedSource(SourceId.From("code-index"), SearchedState.Degraded, "stale", Code: null, AsOf: null);
        var layer = stale with { Reason = "graph", Code = "SND-CODEIDX-020" };

        Assert.Equal(["code-index: stale"], CodeLensSymbols.With(Half(), new CodeSymbolFindings(stale, CodeSymbols.Unavailable, CodeSymbols.Unavailable)).Degraded);
        Assert.Equal(["code-index: SND-CODEIDX-020"], CodeLensSymbols.With(Half(), new CodeSymbolFindings(layer, CodeSymbols.Unavailable, CodeSymbols.Unavailable)).Degraded);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_findings_of_no_index_leave_the_view_as_it_was()
    {
        var view = Half();
        var none = new CodeSymbolFindings(CodeIndexReading.Row(new CodeIndexHead(CodeIndexState.Missing, Commit: null, BuiltAt: null, AsOfCommit: null, Layer: null, Code: null)), CodeSymbols.Unavailable, CodeSymbols.Unavailable);

        Assert.Equal(view.Callers, CodeLensSymbols.With(view, none).Callers);
        Assert.Equal(view.Searched.Sources.Select(s => (s.Id.Value, s.State, s.Reason, s.Code)), CodeLensSymbols.With(view, none).Searched.Sources.Select(s => (s.Id.Value, s.State, s.Reason, s.Code)));
        Assert.Empty(CodeLensSymbols.With(view, none).Degraded);
    }
}
