namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The fixed words the card and the page bind (<see cref="SetupViewModel"/>, continued): each is the app's own copy in <see cref="Labels"/>, a property because the view binds it.</summary>
internal sealed partial class SetupViewModel
{
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The page's name.</summary>
    public string Title => Labels.SetupPage;

    /// <summary>What the page is for, in one line.</summary>
    public string Purpose => Labels.SetupPurpose;

    /// <summary>The card's heading.</summary>
    public string CardHeading => Labels.SetupCardHeading;

    /// <summary>The card's link to the page.</summary>
    public string OpenText => Labels.SetupOpen;

    /// <summary>The link's accessible name.</summary>
    public string OpenName => Labels.SetupOpenName;

    /// <summary>The page before its first answer.</summary>
    public string ReadingText => Labels.ReadingSetup;

    /// <summary>The stale note's words.</summary>
    public string StaleText => Labels.Stale;

    /// <summary>The heading over the steps in order.</summary>
    public string AllStepsText => Labels.SetupAllSteps;

    /// <summary>The disclosure that holds the ids and codes.</summary>
    public string DetailsText => Labels.SetupDetails;

    /// <summary>The retry action's words.</summary>
    public string RetryText => Labels.TryAgain;

    /// <summary>"Sindri isn't running."</summary>
    public string NotRunningText => Labels.SetupNotRunning;

    /// <summary>The line over the launcher command.</summary>
    public string StartItText => Labels.SetupStartIt;

    /// <summary>What starting Sindri does for the app.</summary>
    public string ReconnectsText => Labels.SetupReconnects;

    /// <summary>The line under the finished lead.</summary>
    public string FinishedHint => Labels.SetupFinishedHint;

    /// <summary>What the page says when this Sindri has no setup list.</summary>
    public string NotServedText => Labels.SetupNotServed;

    /// <summary>What the page says when the list can't be read.</summary>
    public string UnreadableText => Labels.SetupUnreadable;
#pragma warning restore CA1822, MA0041
}
