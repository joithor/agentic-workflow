namespace Aw.Judge.Core.Questions;

/// <summary>choose: the one question whose outputs, criteria and threshold come from the call (docs/csharp/judge.md).</summary>
public static class ChooseQuestion
{
    /// <summary>The question's name.</summary>
    public const string Name = "choose";

    /// <summary>The definition for one call's input.</summary>
    /// <param name="input">The call's input (may be invalid; <see cref="QuestionDefinition{TInput}.InputProblem"/> says so).</param>
    /// <returns>The definition.</returns>
    public static QuestionDefinition<ChooseInput> For(ChooseInput input) => throw new NotSupportedException("choose is not built yet");
}
