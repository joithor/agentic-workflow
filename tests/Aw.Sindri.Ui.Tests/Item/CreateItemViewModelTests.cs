using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>Create item (UI spec §4.5, §7: `C` opens a small form, title, repo and body → CreateItem, desk), through the catalog's control.</summary>
[Lane(TestLane.Unit)]
public sealed class CreateItemViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.SpecShaped("commands", "catalog-p3") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<CreateItemViewModel> Form()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var form = new CreateItemViewModel(catalog, () => _api, _server);
        form.Open();
        return form;
    }

    [Fact]
    public async Task The_form_opens_empty_and_needs_a_title_to_send()
    {
        var form = await Form();

        Assert.Equal((true, string.Empty, false), (form.IsOpen, form.Title, form.CanSubmit));
        form.Title = "Proof: appears live";
        Assert.True(form.CanSubmit);
    }

    [Fact]
    public async Task Submit_sends_the_form_once_and_closes_when_the_outcome_settles()
    {
        var form = await Form();
        (form.Title, form.Repo, form.Body) = ("Proof: appears live", "agentic-workflow", " ");

        await form.Submit(Token);

        Assert.Equal([new CreateItemRequest("Proof: appears live", "agentic-workflow", Body: null, Link: null)], _server.Created);
        Assert.Equal(("Create item: done", "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306", true), (form.Control!.Outcome, form.CreatedId, form.IsOpen));

        _clock.Advance(ControlViewModel.AcceptedFor);

        Assert.Equal((false, string.Empty, string.Empty), (form.IsOpen, form.Title, form.Repo));
    }

    // §4.1 Error: a refusal is the API's code and detail inline; the form keeps its text.
    [Fact]
    public async Task A_refused_create_keeps_the_form_and_shows_the_apis_detail()
    {
        _server.Create = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-009", "Sindri is busy with another command (fix: retry)");
        var form = await Form();
        form.Title = "Kept";

        await form.Submit(Token);

        Assert.Equal(("SND-API-009", "Sindri is busy with another command (fix: retry)"), (form.Control!.ErrorCode, form.Control.ErrorDetail));
        Assert.Equal((true, "Kept"), (form.IsOpen, form.Title));
    }

    // §7: until P3 ships the catalog says pending:P3, so the form's button is drawn disabled with its phase and CLI.
    [Fact]
    public async Task Before_p3_the_create_control_is_pending()
    {
        _server.Catalog = RouteAnswer.Api("commands", "catalog");
        var form = await Form();
        form.Title = "Not yet";

        Assert.Equal((true, false), (form.Control!.IsPending, form.CanSubmit));
        Assert.Equal("arrives in P3 · sindri item add <title> [--repo <repo>]", form.Control.PendingLine);

        await form.Submit(Token);
        Assert.Empty(_server.Created);
    }
}
