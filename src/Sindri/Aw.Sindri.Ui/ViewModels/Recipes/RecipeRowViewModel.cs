using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>One workflow template on the Recipes page (recipes spec §1, §2): its plain name, the id in grey, version, standing, size, needs and produces, and fingerprint. Immutable: a re-read that changes a row replaces it.</summary>
internal sealed class RecipeRowViewModel : ObservableObject
{
    /// <summary>The row over what the list served.</summary>
    /// <param name="served">The list's row.</param>
    internal RecipeRowViewModel(RecipeSummaryView served)
    {
        ArgumentNullException.ThrowIfNull(served);
        Id = served.Id;
        Hash = served.Hash;
        Name = RecipeNames.Of(served.Id, served.DisplayName);
        ShortId = served.Id.StartsWith("workflow.", StringComparison.Ordinal) ? served.Id["workflow.".Length..] : served.Id;
        Standing = served.Standing switch
        {
            "approved" => RecipeStanding.Approved,
            "revoked" => RecipeStanding.Revoked,
            _ => RecipeStanding.NotApproved,
        };
        StandingText = Standing switch
        {
            RecipeStanding.Approved => Labels.StandingApproved,
            RecipeStanding.Revoked => Labels.StandingRevoked,
            RecipeStanding.NotApproved => Labels.StandingNotApproved,
            _ => throw new ArgumentOutOfRangeException(nameof(served), Standing, message: null),
        };
        StaleReasons = [.. (served.Stale ?? []).Select(Labels.StaleReason)];
        VersionText = string.Create(CultureInfo.InvariantCulture, $"v{served.Version}");
        StepsText = Labels.StepsWord(served.States?.Count ?? 0);
        FlowText = Labels.FlowWord(served.Inputs?.Count ?? 0, served.Outputs?.Count ?? 0);
        Fingerprint = Labels.Fingerprint(served.Hash);
        AccessibleName = Labels.RecipeRowName(Name, Id, StandingText, IsOutOfDate, served.Version);
        Link = new DeepLink(DeepLinkKind.Recipe, Standing == RecipeStanding.Approved ? Id : Id + "@" + Labels.Hash12(served.Hash));
        Signature = string.Join('|', served.Id, served.Version, served.Hash, served.Standing, served.DisplayName, string.Join(',', served.Stale ?? []), served.States?.Count, served.Inputs?.Count, served.Outputs?.Count);
    }

    /// <summary>The recipe id.</summary>
    public string Id { get; }

    /// <summary>The recipe's hash as served.</summary>
    internal string Hash { get; }

    /// <summary>The plain name.</summary>
    public string Name { get; }

    /// <summary>The id without "workflow.", grey and mono.</summary>
    public string ShortId { get; }

    /// <summary>The version, "v2".</summary>
    public string VersionText { get; }

    /// <summary>Where it stands.</summary>
    public RecipeStanding Standing { get; }

    /// <summary>The standing word.</summary>
    public string StandingText { get; }

    /// <summary>True when the standing is Approved (the badge's style).</summary>
    public bool IsApproved => Standing == RecipeStanding.Approved;

    /// <summary>True when the standing is Revoked (the badge's style).</summary>
    public bool IsRevoked => Standing == RecipeStanding.Revoked;

    /// <summary>True when something it relies on changed since it was approved (or it is in an old format).</summary>
    public bool IsOutOfDate => StaleReasons.Count > 0;

    /// <summary>The second, grey word.</summary>
    public string OutOfDateText { get; } = Labels.OutOfDate;

    /// <summary>Why it is out of date, in words, one per reason served.</summary>
    public IReadOnlyList<string> StaleReasons { get; }

    /// <summary>True when it is not approved, revoked or out of date: the Needs approval filter shows it.</summary>
    public bool NeedsApproval => Standing != RecipeStanding.Approved || IsOutOfDate;

    /// <summary>The size, "8 steps".</summary>
    public string StepsText { get; }

    /// <summary>What it needs and produces, "2 in, 4 out".</summary>
    public string FlowText { get; }

    /// <summary>The short fingerprint.</summary>
    public string Fingerprint { get; }

    /// <summary>The row's accessible name.</summary>
    public string AccessibleName { get; }

    /// <summary>Opens its sheet: an approved workflow by id, another by its hash (the id alone reads the approved version).</summary>
    public DeepLink Link { get; }

    /// <summary>What a re-read compares to keep this very row: everything it shows.</summary>
    internal string Signature { get; }

    /// <summary>True when selected by the keys.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }

    /// <summary>Whether the search finds it by name or id.</summary>
    /// <param name="query">The search text; empty matches all.</param>
    /// <returns>True when the name or the id contains the text, ignoring case.</returns>
    internal bool Matches(string query) =>
        query.Length == 0 || Name.Contains(query, StringComparison.OrdinalIgnoreCase) || Id.Contains(query, StringComparison.OrdinalIgnoreCase);
}
