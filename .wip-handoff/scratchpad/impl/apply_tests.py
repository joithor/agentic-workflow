import re,os
R='/home/thor/repos/aw-wt/sindri-ui-context-2/tests/Aw.Sindri.Ui.Tests/'
# 1 ContextLensStackTests
p=R+'Context/ContextLensStackTests.cs'
s=open(p).read()
a=s.index("    [Fact]\n    public async Task The_bands_edges_coverage_and_sources_are_drawn_from_the_answer()")
b=s.index("    [Fact]\n    public async Task A_missing_anchor_is_named")
new='''    [Fact]
    public async Task The_answer_is_laid_out_as_a_map_of_its_nodes_and_edges()
    {
        var golden = Golden("map-tabs");
        var page = Page(new FakeSindriApi());
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        var bands = golden["bands"]!;
        var map = page.Map!;
        Assert.True(page.HasStack);
        Assert.Equal(3, map.Bands.Count);
        Assert.Equal(bands["intent"]!["records"]!.AsArray().Count + bands["intent"]!["decisions"]!.AsArray().Count + bands["product"]!["own"]!.AsArray().Count + bands["code"]!["files"]!.AsArray().Count, map.Nodes.Count);
        Assert.Equal(bands["product"]!["edges"]!.AsArray().Count + golden["edges"]!.AsArray().Count, map.Edges.Count);   // the conflict replaces one edge
        Assert.Equal(Labels.ContextMapCoverageLead(1, 4), map.CoverageLead);
        Assert.False(page.IsDraft);
    }

    [Fact]
    public async Task A_model_link_is_a_dashed_edge_whose_pill_says_how_sure_the_tool_is_and_a_suggested_file_is_a_suggestion()
    {
        var golden = Golden("map-tabs");
        var edge = golden["edges"]!.AsArray().Single(e => (string)e!["provenance"]! == "model")!;
        var page = Page(new FakeSindriApi());
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        var confidence = (int)Math.Round((double)edge["confidence"]! * 100, MidpointRounding.AwayFromZero);
        var pill = page.Map!.Pills.Single(p => p.Key == (string)edge["link"]!);
        Assert.True(pill.Dashed);
        Assert.EndsWith(Labels.ContextMapSuggestedPct(confidence), pill.Text, StringComparison.Ordinal);
        Assert.Contains(page.Map.Nodes, n => n.IsSuggested && n.Caption.EndsWith(Labels.ContextMapSuggestedPct(confidence), StringComparison.Ordinal));
    }

'''
s=s[:a]+new+s[b:]
a=s.index("    [Fact]\n    public async Task A_truncated_product_band_counts")
b=s.index("    [Fact]\n    public async Task A_read_in_flight_does_not_overwrite")
new='''    [Fact]
    public async Task A_truncated_product_band_draws_one_more_not_shown_chip()
    {
        var golden = Golden("map-tabs");
        var body = golden.DeepClone();
        body["bands"]!["product"]!["truncated"] = true;
        var server = new FakeSindriApi { LensesAnswer = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, body.ToJsonString()) };
        var page = Page(server);
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        Assert.Single(page.Map!.Nodes, n => n.Label == Labels.ContextTruncated);
    }

'''
s=s[:a]+new+s[b:]
s=s.replace("The lens stack (U9a): the anchor box reads the lenses and draws the head, bands, edges, coverage and searched sources.","The lens stack (U9a, now the map of F3 spec 1): the anchor box reads the lenses and draws the head and the map.")
open(p,'w').write(s)
# 2 ContextPageWindowTests
p=R+'Views/ContextPageWindowTests.cs'
s=open(p).read()
a=s.index("    public async Task Typing_an_anchor_draws_the_stack_and_every_line_reaches_the_automation_tree()")
a=s.rindex("    [Fact]",0,a)
b=s.index("    // Each stage's host is its seam view")
new='''    [Fact]
    public async Task Typing_an_anchor_draws_the_map_and_every_node_band_and_label_reaches_the_automation_tree()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextPageWindowTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var shell = new ShellViewModel(records, new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
            await shell.Restore(Token);
            var window = new ShellWindow { DataContext = shell };
            try
            {
                window.Show();
                _ = shell.Run(stop.Token);
                for (var i = 0; i < 200 && shell.State != ShellState.Normal; i++)
                {
                    await Task.Delay(10, Token);
                    Dispatcher.UIThread.RunJobs();
                }

                shell.GoTo(PageId.Context);
                shell.Context.AnchorText = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;
                await shell.Context.Submit(Token);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var heard = Heard.Tree(window).Select(n => n.Name).ToList();
                var map = shell.Context.Map!;
                var expected = map.Nodes.Select(n => n.Name).Concat(map.Bands.Select(b => b.Name)).Concat(map.Pills.Where(p => p.Always).Select(p => p.Text));
                Assert.All(expected, text => Assert.Contains(heard, h => string.Equals(h, text, StringComparison.Ordinal)));
            }
            finally
            {
                await stop.CancelAsync();
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, Token);
    }

'''
s=s[:a]+new+s[b:]
s=s.replace("using Avalonia.Automation;\nusing Avalonia.Controls;","using Avalonia.Automation;\nusing Avalonia.Controls;")
open(p,'w').write(s)
# 3 PlainWordsTests
p=R+'Shell/PlainWordsTests.cs'
s=open(p).read()
s=s.replace('["Look up", "Type what to look up, such as feature:name", "No context map yet", "1 topic", "4 topics", "Why", "What it covers", "Links", "How well it is backed", "What was searched", "More about this answer"]','["Look up", "Type what to look up, such as feature:name", "No context map yet", "1 topic", "4 topics", "Why we\'re doing this", "What it touches in the product", "What it touches in the code", "Suggested by AI, not confirmed", "Why this matters", "No summary written for this map yet."]')
s=s.replace("Labels.ContextHead(4), Labels.ContextIntent, Labels.ContextProduct, Labels.ContextEdges, Labels.ContextCoverage, Labels.ContextSearched, Labels.ContextMore]","Labels.ContextHead(4), Labels.ContextMapBandIntent, Labels.ContextMapBandProduct, Labels.ContextMapBandCode, Labels.ContextMapLegendSuggested, Labels.ContextMapWhyTitle, Labels.ContextMapWhyNone]")
open(p,'w').write(s)
# 4 ThemeResourcesTests
p=R+'Theme/ThemeResourcesTests.cs'
s=open(p).read()
s=s.replace('"Command", "Close"]','"Command", "Close", "Kind.Feature", "Kind.Screen", "Kind.Route", "Kind.Dialog", "Kind.Email", "Kind.Action", "Kind.Data", "Kind.File", "Kind.Test", "Kind.Ticket", "Kind.Decision", "Kind.Dot", "Kind.Warn", "Kind.Chevron", "Kind.Plus", "Kind.Minus", "Kind.Fit"]')
open(p,'w').write(s)
print("tests patched")
