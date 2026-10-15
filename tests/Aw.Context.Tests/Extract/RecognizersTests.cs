using Aw.Context.Core.Extract;

namespace Aw.Context.Tests.Extract;

public sealed class RecognizersTests
{
    private static IReadOnlyList<Found> Find(string path, string text) =>
        [.. Recognizers.All.SelectMany(r => r.Applies(new RepoFile(path, text)) ? r.Find(new RepoFile(path, text)) : [])];

    private static string[] Keys(string path, string text) => [.. Find(path, text).Select(f => f.Key.Value).Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public void A_command_id_union_gives_one_command_surface_per_literal()
    {
        var found = Find("src/commands/ids.ts", "export type CommandId = \"split-vertical\" | \"split-horizontal\" | \"close-pane\";");

        Assert.Equal(["surface:command/close-pane", "surface:command/split-horizontal", "surface:command/split-vertical"], found.Select(f => f.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var split = found.Single(f => f.Key.Value == "surface:command/split-vertical");
        Assert.Equal(("Split vertical", false, 1.0), (split.Label, split.Named, split.Confidence));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_command_registry_entry_names_the_command_with_its_title()
    {
        var found = Find("src/commands/registry.ts", "register({ id: \"toggle-orientation\", title: \"Toggle orientation\" });");

        var command = Assert.Single(found);
        Assert.Equal(("surface:command/toggle-orientation", "Toggle orientation", true), (command.Key.Value, command.Label, command.Named));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_keybinding_table_gives_a_keybinding_surface_that_moves_to_its_command()
    {
        const string Text = "export const keys: Record<string, CommandId> = {\n  \"ctrl+shift+d\": \"split-vertical\",\n  \"ctrl+w\": \"close-pane\",\n};";

        var found = Find("src/keys.ts", Text);

        var binding = Assert.Single(found, f => f.Key.Value == "surface:keybinding/ctrl+shift+d");
        var edge = Assert.Single(binding.Edges);
        Assert.Equal(("surface:command/split-vertical", Aw.Context.Core.Graph.EdgeType.MovesTo), (edge.To.Value, edge.Type));
        Assert.Equal(2, found.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_settings_json_gives_one_setting_surface_per_top_level_key()
    {
        Assert.Equal(["surface:setting/paneResizeStep", "surface:setting/restoreLayout"],
            Keys("config/settings.json", "{\"paneResizeStep\": 8, \"restoreLayout\": true}"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_json_file_that_is_not_a_settings_file_or_not_json_gives_nothing()
    {
        Assert.Empty(Keys("package.json", "{\"name\": \"x\"}"));
        Assert.Empty(Keys("config/settings.json", "not json"));
        Assert.Empty(Keys("config/settings.json", "[1,2]"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Routes_give_a_route_surface_per_method_and_path()
    {
        Assert.Equal(["surface:route/GET:/api/panes", "surface:route/POST:/api/panes"],
            Keys("src/server.ts", "app.get('/api/panes', h);\nrouter.post(\"/api/panes\", h);"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_component_gets_a_pane_dialog_or_screen_surface_by_its_name()
    {
        const string Text = "export function TabStrip() {}\nexport const RestoreDialog = () => null;\nexport default class SettingsScreen {}";

        Assert.Equal(["surface:dialog/RestoreDialog", "surface:pane/TabStrip", "surface:screen/SettingsScreen"], Keys("src/ui/Tabs.tsx", Text));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_component_is_only_read_from_a_jsx_file() =>
        Assert.Empty(Keys("src/ui/helpers.ts", "export function TabStrip() {}"));

    [Fact, Trait("Lane", "unit")]
    public void A_test_id_gives_a_pane_surface() =>
        Assert.Equal(["surface:pane/split-handle"], Keys("src/ui/Split.tsx", "<div data-testid=\"split-handle\" />"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/a.ts", true)]
    [InlineData("src/ui/A.tsx", true)]
    [InlineData("config/settings.json", true)]
    [InlineData("src/a.test.ts", false)]
    [InlineData("src/a.spec.tsx", false)]
    [InlineData("tests/a.ts", false)]
    [InlineData("src/__tests__/a.ts", false)]
    [InlineData("node_modules/x/a.ts", false)]
    [InlineData("dist/a.js", false)]
    [InlineData("README.md", false)]
    [InlineData("src/a.cs", false)]
    public void Only_source_files_outside_tests_and_build_output_are_read(string path, bool expected) =>
        Assert.Equal(expected, Recognizers.Source(new RepoFile(path, "x")));

    [Fact, Trait("Lane", "unit")]
    public void A_huge_file_is_not_read() =>
        Assert.False(Recognizers.Source(new RepoFile("src/big.ts", new string('x', 600_000))));

    [Fact, Trait("Lane", "unit")]
    public void A_surface_name_with_unusable_characters_is_dropped_not_thrown()
    {
        Assert.Empty(Keys("src/commands/ids.ts", "export type CommandId = \"bad\\u0001id\";"));
        Assert.Empty(Keys("src/server.ts", "app.get('/a b', h);"));
    }
}
