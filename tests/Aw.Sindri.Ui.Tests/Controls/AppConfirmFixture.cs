using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The shared confirmation controls over a catalog the test shapes: each of the three approvals ready or not, and challenges as the server serves them.</summary>
internal sealed class AppConfirmFixture
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private static readonly string[] _actions = ["ApproveProfile", "ApproveRecipe", "ApproveAllRecipes", "Decide", "ApplyCommand:answer-grant-tokens"];

    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    internal FakeClipboard Clipboard { get; } = new();

    /// <summary>A catalog whose rows for the three approvals say <paramref name="ui"/>.</summary>
    internal CatalogViewModel Catalog(string ui = "ready")
    {
        var catalog = new CatalogViewModel(Clipboard, Clock);
        catalog.Show(_api, new FakeSindriApi(), new CommandList([.. _actions.Select(a => Row(a, ui))]));
        return catalog;
    }

    /// <summary>Reads the catalog again with the three approvals' rows saying <paramref name="ui"/> (it loaded after a row showed).</summary>
    internal static void Reread(CatalogViewModel catalog, string ui) => catalog.Show(_api, new FakeSindriApi(), new CommandList([.. _actions.Select(a => Row(a, ui))]));

    internal AppChallengeView Typed(string action = "approve profile 3f9a1c") =>
        new("CHL-00000000000000000000000000000001", action, Clock.GetUtcNow().AddMinutes(5), "typed", MinHoldMs: null, TypedAlternative: null);

    internal AppChallengeView Held(int minHoldMs = 1500) =>
        new("CHL-00000000000000000000000000000002", "approve-all", Clock.GetUtcNow().AddMinutes(5), "held", minHoldMs, TypedAlternative: "approve-all 1c1fd4");

    private static CommandRowView Row(string action, string ui) =>
        new(action, "protected", ["human"], Terminal: true, "POST", "/sindri/profile/approve", "sindri profile approve", ui, Destructive: false, ReadsViews: false, Shortcut: null);
}
