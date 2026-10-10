namespace Aw.Judge.Host.Commands;

/// <summary>
/// Any first word the table doesn't route is a question (TS cli.ts's default branch): stdin must be JSON, then the
/// question is looked up. No question is ported yet, so every name is unknown.
/// </summary>
internal static class QuestionCommand
{
    internal static async Task<int> Run(string name)
    {
        using var input = CommandOutput.ParseInput(await CommandOutput.ReadInput(CancellationToken.None).ConfigureAwait(false));
        return input is null
            ? CommandOutput.Emit(1, "", "input is not valid JSON")
            : CommandOutput.Emit(1, "", $"unknown question: {name}");
    }
}
