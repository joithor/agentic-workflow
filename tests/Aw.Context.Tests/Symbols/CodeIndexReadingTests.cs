using Aw.Context.Core.Lenses;
using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Symbols;

namespace Aw.Context.Tests.Symbols;

public sealed class CodeIndexReadingTests
{
    private const string Indexed = CodeSymbolSourceContract.Indexed;
    private const string Other = CodeSymbolSourceContract.Other;
    private static readonly DateTimeOffset _built = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static CodeIndexHead Head(CodeIndexState state, string? asOf = null, string? layer = null, string? code = null) =>
        CodeIndexReading.Head(new CodeIndexHead(state, Indexed, _built, AsOfCommit: null, layer, code), asOf);

    [Theory, Trait("Lane", "unit")]
    [InlineData("7c3e1a2", Indexed, true)]
    [InlineData(Indexed, "7c3e1a2", true)]
    [InlineData(Indexed, Indexed, true)]
    [InlineData(Indexed, Other, false)]
    [InlineData("7c3e1a2", Other, false)]
    [InlineData("7c3e1", Indexed, false)]   // shorter than seven characters is not a commit name
    public void Same_commit_allows_one_to_be_abbreviated(string left, string right, bool same) => Assert.Equal(same, CodeIndexReading.Same(left, right));

    [Fact, Trait("Lane", "unit")]
    public void A_readable_index_at_another_commit_than_the_maps_is_stale_with_both_commits()
    {
        var head = Head(CodeIndexState.Ok, asOf: Other);

        Assert.Equal((CodeIndexState.Stale, Indexed, Other), (head.State, head.Commit, head.AsOfCommit));
        Assert.True(CodeIndexReading.AsOfMismatch(head));
        Assert.Equal((SymbolsState.Stale, Other), CodeIndexReading.Stamp(head));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_index_at_the_maps_commit_is_ok_and_its_rows_name_no_as_of_commit()
    {
        var head = Head(CodeIndexState.Ok, asOf: Indexed[..7]);

        Assert.Equal(CodeIndexState.Ok, head.State);
        Assert.False(CodeIndexReading.AsOfMismatch(head));
        Assert.Equal((SymbolsState.Ok, (string?)null), CodeIndexReading.Stamp(head));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_stale_head_with_no_as_of_commit_stamps_stale_without_one()
    {
        var head = Head(CodeIndexState.Stale);

        Assert.Equal((SymbolsState.Stale, (string?)null), CodeIndexReading.Stamp(head));
        Assert.Equal((SymbolsState.Stale, (string?)null), CodeIndexReading.Stamp(Head(CodeIndexState.CheckoutNotOnMain)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(CodeIndexState.Missing)]
    [InlineData(CodeIndexState.Damaged)]
    [InlineData(CodeIndexState.Building)]
    [InlineData(CodeIndexState.NotConfigured)]
    public void An_unreadable_index_keeps_its_state_whatever_the_as_of_commit(CodeIndexState state) =>
        Assert.Equal(state, CodeIndexReading.Head(new CodeIndexHead(state, Commit: null, BuiltAt: null, AsOfCommit: null, Layer: null, Code: null), Other).State);

    [Fact, Trait("Lane", "unit")]
    public void A_layer_that_is_down_at_the_maps_commit_stays_a_layer_problem_and_at_another_commit_is_stale()
    {
        Assert.Equal(CodeIndexState.LayerUnavailable, Head(CodeIndexState.LayerUnavailable, asOf: Indexed, layer: "graph").State);

        var behind = Head(CodeIndexState.LayerUnavailable, asOf: Other, layer: "graph", code: "SND-CODEIDX-020");

        Assert.Equal((CodeIndexState.Stale, "graph", "SND-CODEIDX-020"), (behind.State, behind.Layer, behind.Code));
    }

    // The table of spec §7.3, "lens mapping".
    [Theory, Trait("Lane", "unit")]
    [InlineData(CodeIndexState.Ok, SearchedState.Ok, null, null)]
    [InlineData(CodeIndexState.Stale, SearchedState.Degraded, "stale", null)]
    [InlineData(CodeIndexState.CheckoutNotOnMain, SearchedState.Degraded, "checkout-not-on-main", null)]
    [InlineData(CodeIndexState.LayerUnavailable, SearchedState.Degraded, "graph", "SND-CODEIDX-020")]
    [InlineData(CodeIndexState.Missing, SearchedState.Unavailable, "index-not-built", "SND-CODEIDX-013")]
    [InlineData(CodeIndexState.Damaged, SearchedState.Unavailable, "index-damaged", "SND-CODEIDX-010")]
    [InlineData(CodeIndexState.Building, SearchedState.Unavailable, "index-building", null)]
    [InlineData(CodeIndexState.NotConfigured, SearchedState.NotConfigured, "not-configured", null)]
    public void The_lens_row_maps_the_blocks_state(CodeIndexState state, SearchedState searched, string? reason, string? code)
    {
        var readable = state is not (CodeIndexState.Missing or CodeIndexState.Damaged or CodeIndexState.Building or CodeIndexState.NotConfigured);
        var row = CodeIndexReading.Row(CodeIndexReading.Head(new CodeIndexHead(state, readable ? Indexed : null, _built, AsOfCommit: null, state == CodeIndexState.LayerUnavailable ? "graph" : null, code), asOf: null));

        Assert.Equal(("code-index", searched, reason, code), (row.Id.Value, row.State, row.Reason, row.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_as_of_mismatch_is_a_degraded_stale_row_as_of_the_build()
    {
        var row = CodeIndexReading.Row(Head(CodeIndexState.Ok, asOf: Other));

        Assert.Equal((SearchedState.Degraded, "stale", (string?)null, (DateTimeOffset?)_built), (row.State, row.Reason, row.Code, row.AsOf));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_index_without_a_code_is_the_row_the_lens_had_before_the_port()
    {
        var row = CodeIndexReading.Row(new CodeIndexHead(CodeIndexState.Missing, Commit: null, BuiltAt: null, AsOfCommit: null, Layer: null, Code: null));

        Assert.Equal(("code-index", SearchedState.Unavailable, "index-not-built", (string?)null, (DateTimeOffset?)null), (row.Id.Value, row.State, row.Reason, row.Code, row.AsOf));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_read_is_a_degraded_row_with_the_errors_code()
    {
        var row = CodeIndexReading.FailedRow("SND-CODEIDX-020");

        Assert.Equal(("code-index", SearchedState.Degraded, "SND-CODEIDX-020"), (row.Id.Value, row.State, row.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public void Calls_of_a_missing_index_are_the_unavailable_the_lens_had_before_the_port()
    {
        var calls = CodeIndexReading.Calls(new CodeIndexHead(CodeIndexState.Missing, null, null, null, null, null), [], 0);

        Assert.Equal(Core.Code.CodeSymbols.Unavailable, calls);
    }

    [Fact, Trait("Lane", "unit")]
    public void Calls_of_a_damaged_or_building_index_name_why()
    {
        Assert.Equal((SymbolsState.Unavailable, "index-damaged", "SND-CODEIDX-010"),
            Pick(CodeIndexReading.Calls(new CodeIndexHead(CodeIndexState.Damaged, null, null, null, null, "SND-CODEIDX-010"), [], 0)));
        Assert.Equal((SymbolsState.Unavailable, "index-building", (string?)null),
            Pick(CodeIndexReading.Calls(new CodeIndexHead(CodeIndexState.Building, null, null, null, null, null), [], 0)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Calls_over_an_index_at_the_maps_commit_are_ok_with_the_commit_and_the_unresolved_count()
    {
        var calls = CodeIndexReading.Calls(Head(CodeIndexState.Ok, asOf: Indexed), [Edge(SymbolsState.Ok, asOf: null)], unresolved: 3);

        Assert.Equal((SymbolsState.Ok, (string?)null, Indexed, (string?)null, false, 3, 1), (calls.State, calls.Reason, calls.Commit, calls.AsOfCommit, calls.AsOfMismatch, calls.Unresolved, calls.Items.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void Calls_over_an_index_at_another_commit_are_stale_naming_both()
    {
        var calls = CodeIndexReading.Calls(Head(CodeIndexState.Ok, asOf: Other), [Edge(SymbolsState.Stale, Other)], unresolved: 0);

        Assert.Equal((SymbolsState.Stale, "stale", Indexed, Other, true), (calls.State, calls.Reason, calls.Commit, calls.AsOfCommit, calls.AsOfMismatch));
    }

    [Fact, Trait("Lane", "unit")]
    public void Calls_while_the_graph_layer_is_down_are_ok_with_a_note()
    {
        var calls = CodeIndexReading.Calls(Head(CodeIndexState.LayerUnavailable, asOf: Indexed, layer: "graph"), [Edge(SymbolsState.Ok, null)], 0);

        Assert.Equal((SymbolsState.Ok, "graph-unavailable"), (calls.State, calls.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_file_map_column_of_a_missing_index_is_the_unavailable_the_map_had_before_the_port() =>
        Assert.Equal(LensSymbols.Unavailable, CodeIndexReading.FileMap(new CodeIndexHead(CodeIndexState.Missing, null, null, null, null, null), []));

    [Fact, Trait("Lane", "unit")]
    public void The_file_map_column_names_why_it_is_empty()
    {
        Assert.Equal((SymbolsState.Unavailable, "index building"), Words(CodeIndexReading.FileMap(new CodeIndexHead(CodeIndexState.Building, null, null, null, null, null), [])));
        Assert.Equal((SymbolsState.Unavailable, "index damaged"), Words(CodeIndexReading.FileMap(new CodeIndexHead(CodeIndexState.Damaged, null, null, null, null, "SND-CODEIDX-010"), [])));
        Assert.Equal((SymbolsState.Unavailable, "not configured"), Words(CodeIndexReading.FileMap(new CodeIndexHead(CodeIndexState.NotConfigured, null, null, null, null, null), [])));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_file_map_column_over_a_stale_index_names_both_commits()
    {
        var column = CodeIndexReading.FileMap(Head(CodeIndexState.Ok, asOf: Other), [File(SymbolsState.Stale, Other)]);

        Assert.Equal((SymbolsState.Stale, "stale", Indexed, Other, true, 1), (column.State, column.Reason, column.Commit, column.AsOfCommit, column.AsOfMismatch, column.Files!.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_file_map_column_over_a_fresh_index_is_ok_with_the_files()
    {
        var column = CodeIndexReading.FileMap(Head(CodeIndexState.Ok, asOf: Indexed), [File(SymbolsState.Ok, null)]);

        Assert.Equal((SymbolsState.Ok, (string?)null, Indexed, (string?)null, false, 1), (column.State, column.Reason, column.Commit, column.AsOfCommit, column.AsOfMismatch, column.Files!.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_worst_head_of_several_repos_limits_the_answer()
    {
        var ok = Head(CodeIndexState.Ok);
        var stale = Head(CodeIndexState.Stale);
        var missing = new CodeIndexHead(CodeIndexState.Missing, null, null, null, null, null);

        Assert.Equal(CodeIndexState.Missing, CodeIndexReading.Worst([ok, missing, stale]).State);
        Assert.Equal(CodeIndexState.Stale, CodeIndexReading.Worst([ok, stale]).State);
        Assert.Equal(CodeIndexState.Ok, CodeIndexReading.Worst([ok]).State);
    }

    private static (SymbolsState, string?, string?) Pick(Core.Code.CodeSymbols s) => (s.State, s.Reason, s.Code);

    private static (SymbolsState, string?) Words(LensSymbols s) => (s.State, s.Reason);

    private static CallEdgeRow Edge(SymbolsState state, string? asOf) =>
        new(CodeSymbolSourceContract.Symbols[0], "semantic", 1.0, "full", 1, Indexed, asOf, state);

    private static FileSymbolsRow File(SymbolsState state, string? asOf) =>
        new("src/layout/SplitPane.ts", [CodeSymbolSourceContract.Symbols[0]], Indexed, asOf, state);
}
