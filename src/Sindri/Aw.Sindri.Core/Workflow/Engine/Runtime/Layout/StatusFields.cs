namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The WF 6.4 status layout as fields (E-23); Text is the CLI form, from the same renderer.</summary>
/// <param name="Header">The header line's parts.</param>
/// <param name="Next">The rendered 'next' sentence, whole; null when the layout has none.</param>
/// <param name="Answer">The suggested answer line.</param>
/// <param name="Child">The child a park mirrors.</param>
/// <param name="Drift">A recorded drift.</param>
/// <param name="Inputs">The Step's inputs.</param>
/// <param name="Timeout">The state's timeout.</param>
/// <param name="Budget">The token budgets.</param>
/// <param name="Paused">What a takeover paused.</param>
/// <param name="Commands">The instance's own verbs first, then the rest.</param>
/// <param name="Text">The CLI text form, unchanged.</param>
public sealed record StatusFields(
    HeaderParts Header,
    string? Next,
    AnswerLine? Answer,
    ChildPark? Child,
    DriftNote? Drift,
    IReadOnlyList<StepInput> Inputs,
    TimeoutLine? Timeout,
    BudgetLine? Budget,
    PausedLine? Paused,
    IReadOnlyList<CommandRow> Commands,
    string Text);
