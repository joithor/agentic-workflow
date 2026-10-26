namespace Aw.Judge.Core.Questions;

/// <summary>
/// model-pick (docs/csharp/judge.md): which of the offered models is the cheapest that will pass a step. The outputs
/// are the candidates' keys (m1, m2, ... cheapest first), different per call, so the definition is built from the input
/// like choose's. The chain is fixed to Jev then rules, and no file text is stored.
/// </summary>
public static class ModelPickQuestion
{
    /// <summary>The question's name.</summary>
    public const string Name = "model-pick";

    /// <summary>The confidence threshold (same as choose); config.json's entry still wins.</summary>
    public const double DefaultThreshold = 0.6;

    /// <summary>The most candidates a call may offer.</summary>
    public const int MaxCandidates = 32;

    /// <summary>How long a pick may hold a launch.</summary>
    public static TimeSpan Budget { get; } = TimeSpan.FromSeconds(20);

    /// <summary>The cap on the brief and the files' text together, in characters. M-0 replaces this with the measured cap.</summary>
    public const int PackCap = 100_000;

    /// <summary>The definition for a call whose input didn't deserialize: no outputs, the evaluator escalates it as 'invalid-input'.</summary>
    public static QuestionDefinition<ModelPickInput> Unreadable => throw new NotSupportedException("model-pick is not built yet");

    /// <summary>The definition for one call's input.</summary>
    /// <param name="input">The call's input (may be invalid; <see cref="QuestionDefinition{TInput}.InputProblem"/> says so).</param>
    /// <returns>The definition.</returns>
    public static QuestionDefinition<ModelPickInput> For(ModelPickInput input) => throw new NotSupportedException("model-pick is not built yet");
}
