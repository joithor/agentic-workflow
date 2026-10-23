namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): what a desk step asks the person to read, the agent session behind it and the code behind a step's state (engine q65). Plain words, no em dashes.</summary>
internal static partial class Labels
{
    /// <summary>The button for what a desk step asks the person to read: "Open the design". A kind this build doesn't know opens the fact, so it reads as that.</summary>
    /// <param name="kind">design, plan, scope, packet, pr, questions or text; anything else as served.</param>
    /// <returns>The button's words.</returns>
    internal static string ReviewButton(string kind) => kind switch
    {
        "design" => "Open the design",
        "plan" => "Open the plan",
        "scope" => "Open the scope",
        "packet" => "Open the packet",
        "pr" => "Open the PR",
        "questions" => "Open the questions",
        "text" => "Open the text",
        _ => "Open the " + kind,
    };

    /// <summary>The button's accessible name: the words, then what opens.</summary>
    /// <param name="button">The button's words.</param>
    /// <param name="fact">The fact it stands for.</param>
    /// <returns>The name.</returns>
    internal static string ReviewButtonName(string button, string fact) => $"{button}, from {fact}";

    /// <summary>Why a step stands as it does, for the codes this build knows; an unknown code reads as itself.</summary>
    /// <param name="code">The served code (SND-WF-010).</param>
    /// <returns>The words, or the code.</returns>
    internal static string ReasonCodeWords(string code) => code switch
    {
        "SND-WF-010" => "The step's dispatch was refused",
        _ => code,
    };
}
