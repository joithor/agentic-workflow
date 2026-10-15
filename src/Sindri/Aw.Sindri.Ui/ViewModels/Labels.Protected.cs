namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the protected-action lock button and its popover (screens fixture "The protected-action label").</summary>
internal static partial class Labels
{
    /// <summary>The lock button's title (the fixture's sentence, no full stop).</summary>
    internal const string ProtectedTitle = "Needs a typed confirmation at a terminal";

    /// <summary>The popover's Open in an agent.</summary>
    internal const string OpenInAgent = "Open in an agent";

    /// <summary>The lock button's label: the lock glyph, the verb, a middle dot and "terminal". Never prose, never a dash.</summary>
    /// <param name="verb">The verb ("Accept", "Grant rounds").</param>
    /// <returns>The label.</returns>
    internal static string ProtectedLabel(string verb) => $"⌂ {verb} · terminal";

    /// <summary>The lock button's accessible name, which says what it acts on.</summary>
    /// <param name="verb">The verb.</param>
    /// <param name="subject">What it acts on.</param>
    /// <returns>The name.</returns>
    internal static string ProtectedName(string verb, string subject) => $"{verb} {subject}: needs a typed confirmation at a terminal";

    /// <summary>Open in an agent's accessible name, which carries the subject and what it waits for.</summary>
    /// <param name="subject">What it acts on.</param>
    /// <returns>The name.</returns>
    internal static string OpenInAgentFor(string subject) => $"Open {subject} in an agent: available with API P10a";
}
