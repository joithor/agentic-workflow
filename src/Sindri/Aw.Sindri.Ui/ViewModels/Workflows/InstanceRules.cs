using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The rules the Home strip's row and the Workflows page's row share about one served <see cref="InstanceSummary"/>: what the engine's answer and actions allow. Nothing here is worked out from the recipe.</summary>
internal static class InstanceRules
{
    private const string ProtectedClass = "protected";
    private const string CompleteStep = "CompleteStep";

    /// <summary>True when the row's answer is protected: it is the lock button, never a form.</summary>
    /// <param name="row">The served row.</param>
    /// <returns>True for a protected answer.</returns>
    internal static bool HasProtectedAnswer(InstanceSummary row) => row.Answer is { } answer && string.Equals(answer.Class, ProtectedClass, StringComparison.Ordinal);

    /// <summary>True when the row's current state is a desk state and the engine lists CompleteStep for it.</summary>
    /// <param name="row">The served row.</param>
    /// <returns>True when the human can complete the desk step.</returns>
    internal static bool CompletesAtDesk(InstanceSummary row) =>
        row.Actions.Contains(CompleteStep, StringComparer.Ordinal) && row.States.Any(s => s.Desk && string.Equals(s.State, row.State, StringComparison.Ordinal));
}
