using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One workflow under the workflows step: its plain name and where its approval stands, "approved", "approved v4, v5 available" or "Out of date". It decides nothing the list did not serve.</summary>
internal sealed class SetupRecipeLineViewModel
{
    /// <summary>The line over what the list served.</summary>
    /// <param name="served">The workflow's row, at its highest version.</param>
    internal SetupRecipeLineViewModel(RecipeSummaryView served)
    {
        ArgumentNullException.ThrowIfNull(served);
        Name = RecipeNames.Of(served.Id, served.DisplayName);
        var approved = string.Equals(served.Standing, "approved", StringComparison.Ordinal);
        var newer = served.ApprovedVersion is { } was && was < served.Version && !string.Equals(served.Standing, "revoked", StringComparison.Ordinal);   // the shipped version waits while an older one stays approved
        IsOutdated = newer || (approved && (served.Stale is { Count: > 0 } || (served.ApprovedHash is { } hash && !string.Equals(hash, served.Hash, StringComparison.Ordinal))));
        Status = newer ? Labels.SetupRecipeNewer(served.ApprovedVersion!.Value, served.Version)   // why: newer is true only when ApprovedVersion has a value
            : !approved ? Labels.StandingWord(string.Equals(served.Standing, "revoked", StringComparison.Ordinal) ? RecipeStanding.Revoked : RecipeStanding.NotApproved)
            : IsOutdated ? Labels.SetupRecipeOutOfDate : Labels.SetupRecipeApproved;
        AccessibleName = Name + ": " + Status;
    }

    /// <summary>The plain name.</summary>
    public string Name { get; }

    /// <summary>Where its approval stands, in words.</summary>
    public string Status { get; }

    /// <summary>True when it is approved but older than what ships (or changed since).</summary>
    public bool IsOutdated { get; }

    /// <summary>The row's accessible name.</summary>
    public string AccessibleName { get; }
}
