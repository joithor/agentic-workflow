using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>A question and one stored input made ready to ask a provider outside the chain: the reference with its prompt, the input, the time budget.</summary>
public sealed class PreparedQuestion
{
    /// <summary>Makes one.</summary>
    /// <param name="reference">The question with the prompt built for the input.</param>
    /// <param name="input">The validated input as JSON.</param>
    /// <param name="timeBudget">How long one provider may take.</param>
    public PreparedQuestion(QuestionRef reference, JsonNode? input, TimeSpan timeBudget)
    {
        Reference = reference;
        Input = input;
        TimeBudget = timeBudget;
    }

    /// <summary>The question with the prompt built for the input.</summary>
    public QuestionRef Reference { get; }

    /// <summary>The validated input as JSON.</summary>
    public JsonNode? Input { get; }

    /// <summary>How long one provider may take.</summary>
    public TimeSpan TimeBudget { get; }
}
