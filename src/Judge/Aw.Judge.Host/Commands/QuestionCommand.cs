namespace Aw.Judge.Host.Commands;

/// <summary>
/// Any first word the table doesn't route is a question. No question is ported yet (Task 6), so every name is unknown:
/// exit 1 'unknown question: &lt;name&gt;'.
/// </summary>
internal static class QuestionCommand
{
    internal static int Run(string name) => CommandOutput.Emit(1, "", $"unknown question: {name}");
}
