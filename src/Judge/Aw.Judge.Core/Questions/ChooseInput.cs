using System.Text.Json;

namespace Aw.Judge.Core.Questions;

/// <summary>choose's input: a question, the facts it reads, and the choices (each with what it means), asked per call.</summary>
/// <param name="Prompt">The question (must not be blank).</param>
/// <param name="State">The facts the question reads, by key (must not be empty).</param>
/// <param name="Choices">Two to 32 choices; their names are the decision's allowed values.</param>
/// <param name="Threshold">The call's confidence threshold, above 0 and at most 1; config.json's entry wins, else 0.6.</param>
public sealed record ChooseInput(string Prompt, IReadOnlyDictionary<string, JsonElement> State, IReadOnlyList<ChoiceInput> Choices, double? Threshold = null);
