namespace Aw.Evidence.Core.Scripts;

/// <summary>One scripted browser step and the state that must hold after it.</summary>
/// <param name="Action">What the step does.</param>
/// <param name="Target">A path for goto, a test id for click and fill, visible text for expect-visible.</param>
/// <param name="ExpectedState">The state checked after the action.</param>
/// <param name="Value">The text a fill types; null types nothing.</param>
public sealed record ScriptStep(StepAction Action, string Target, ExpectedState ExpectedState, string? Value = null)
{
    /// <summary>The action's name as scripts write it.</summary>
    public string ActionName => NameOf(Action);

    /// <summary>An action's name as scripts write it.</summary>
    /// <param name="action">The action.</param>
    /// <returns>'goto', 'click', 'fill' or 'expect-visible'.</returns>
    public static string NameOf(StepAction action) => action switch
    {
        StepAction.Goto => "goto",
        StepAction.Click => "click",
        StepAction.Fill => "fill",
        StepAction.ExpectVisible => "expect-visible",
        _ => "unknown",
    };

    /// <summary>A description of the step and its expectation, for the selector-repair prompt.</summary>
    /// <returns>The description.</returns>
    public string Describe() => $"{ActionName} {Target}; expected: {ExpectedState.Describe()}";
}
