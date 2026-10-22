namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the confirmations in the setup rows (F3 setup spec §2.1, §2.2). No em dashes, no internal terms.</summary>
internal static partial class Labels
{
    /// <summary>Said when the settings changed while the person was reading them: the phrase was tied to the old list, so a new one is asked for.</summary>
    internal const string SetupProfileChanged = "This changed while you were reading it. Read it again, then approve.";

    /// <summary>Said while the settings the approval is tied to are not known yet.</summary>
    internal const string SetupProfileReading = "Sindri is reading your settings.";

    /// <summary>The line under the approve-all hold and its typed alternative: what a hold or the phrase does.</summary>
    internal const string SetupWorkflowsEvery = "Every workflow passed all of its own checks when Sindri was built.";
}
