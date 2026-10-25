using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The concept band (<see cref="ContextPageViewModel"/>, continued): the map opens on the product's concepts, a concept opens today's bands scoped to it,
/// and the two levels below work as before. A map with no concepts, or a server without the route, opens the bands unscoped as it always did.
/// </summary>
internal sealed partial class ContextPageViewModel
{
    private string? _concept;
    private bool _conceptsRead;

    /// <summary>The first layer of the map: the product's concepts.</summary>
    public ConceptLayerViewModel Concepts { get; }

    /// <summary>Whether the concept layer is on the stage: it has concepts (or is reading them), none is open, and no query's neighbourhood, No map state or build covers it.</summary>
    public bool ShowConcepts => ShowMap && Concepts.IsOnStage && _concept is null && !HasStack && !NoMap && !IsBuilding;

    /// <summary>The id of the concept opened; null on the concept layer and when the map has no concepts.</summary>
    internal string? OpenedConceptId => _concept;

    /// <summary>The back button's words.</summary>
    public string BackText { get; } = Labels.ContextConceptsBack;

    // Registered once: the whole map's crumbs and back button ask the page to go up.
    private void InitConcepts()
    {
        Whole.ConceptsRequested += (_, _) => CloseConcept();
        Whole.UpRequested += (_, _) => _ = Up();
    }

    // The page's first read: the concepts; with some, the layer is the page, else the whole map is read as before.
    private async Task OpenFirst(CancellationToken cancellationToken)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        Concepts.Loading();
        ChangedConcepts();
        var read = await Ports.Api.ContextConcepts(api, _repo ?? string.Empty, cancellationToken).ConfigureAwait(true);
        _conceptsRead = read.IsSuccess || !string.Equals(read.Error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal);
        NoteConcepts(read);
        if (Concepts.HasConcepts)
        {
            Head = Labels.ContextConceptsHead;
            ChangedConcepts();
            ChangedWhole();
            return;
        }

        ChangedConcepts();
        await ReadWhole(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Reads the page's top again (Retry, and when a build has ended): the concepts when none is open, else the open concept's map.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal Task ReadMapTop(CancellationToken cancellationToken) => _concept is null ? OpenFirst(cancellationToken) : ReadWhole(cancellationToken);

    private void NoteConcepts(Result<ContextConceptsView> read)
    {
        if (read.IsError)
        {
            if (IsNotServed(read.Error))
            {
                Concepts.NotServed();
            }
            else
            {
                Concepts.Unavailable(read.Error.Code);
            }

            return;
        }

        switch (read.Value.State)
        {
            case "ok":
                Concepts.Show(read.Value);
                break;
            case "building":
                Concepts.Building();
                break;
            case "unavailable":
                Concepts.Unavailable(read.Value.Code ?? SindriUiErrors.BadAnswer.Code);
                break;
            default:
                Concepts.Empty();
                break;
        }
    }

    // A server without the route answers 404: a refusal of kind not found, or the client's own "answered 404".
    private static bool IsNotServed(Error error) =>
        error.Kind == ErrorKind.NotFound || (string.Equals(error.Code, SindriUiErrors.BadStatus.Code, StringComparison.Ordinal) && error.Message.Contains("404", StringComparison.Ordinal));

    /// <summary>Opens a concept: the three bands, scoped to it.</summary>
    /// <param name="id">The concept's id ("other" for the unassigned nodes).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the scoped map is drawn.</returns>
    internal Task OpenConcept(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var name = Concepts.Find(id)?.Name ?? id;
        CloseInspector();
        ForgetRead();
        AnchorText = string.Empty;
        _ = Concepts.EndSearch();
        _concept = id;
        Whole.EnterConcept(name);
        ChangedConcepts();
        return ReadWhole(cancellationToken);
    }

    /// <summary>Goes back to the concepts: the scoped map goes and the layer shows again.</summary>
    internal void CloseConcept()
    {
        if (_concept is null)
        {
            return;
        }

        CloseInspector();
        Whole.LeaveConcept();
        _concept = null;
        AnchorText = string.Empty;
        ForgetRead();
        Head = Labels.ContextConceptsHead;
        ChangedConcepts();
        ChangedWhole();
        OnChanged(nameof(IsZoomedIn));
    }

    /// <summary>Goes up one layer: a node's panel, a query, an open feature, then the concept.</summary>
    /// <returns>True when it went up.</returns>
    internal bool Up()
    {
        if (Back())
        {
            return true;
        }

        if (Whole.IsAreaOpen)
        {
            MapHome();
            return true;
        }

        if (_concept is null)
        {
            return false;
        }

        CloseConcept();
        return true;
    }

    // A query at the concept layer: the concepts whose name or summary hold the words, and the ones that hold a node the words (or the anchors they resolve to) name.
    private async Task SearchConcepts(Uri api, List<string> words, int generation, CancellationToken cancellationToken)
    {
        var matches = new HashSet<string>(Concepts.Matching(AnchorText.Trim()), StringComparer.Ordinal);
        var resolved = await Resolve(api, words, generation, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }

        if (resolved is { } found)
        {
            var located = await Ports.Api.WholeMap(api, new WholeMapQuery("areas", Area: null, [.. found.Anchors.Take(MaxLocate)], MapVersion: null, Draft: false, Repo: _repo), cancellationToken).ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }

            if (located.IsSuccess)
            {
                matches.UnionWith((located.Value.Located ?? []).Select(l => l.Concept).OfType<string>());
            }
        }

        Concepts.ApplySearch(matches);
        ChangedConcepts();
    }

    private void ChangedConcepts()
    {
        OnChanged(nameof(ShowConcepts));
        OnChanged(nameof(ShowWhole));
        OnChanged(nameof(ShowWholeStatus));
        OnChanged(nameof(OpenedConceptId));
        OnChanged(nameof(Head));
        OnChanged(nameof(HasHead));
        OnChanged(nameof(SearchNote));
        OnChanged(nameof(HasSearchNote));
    }
}
