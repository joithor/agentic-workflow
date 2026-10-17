using System.Text.Json.Serialization;
using Aw.Judge.Core.Evaluation;

namespace Aw.Judge.Host.Commands;

/// <summary>An escalated question's stdout (exit 2; ask-check exits 0); choose adds the decision id and any probabilities.</summary>
/// <param name="Escalated">Always true: tells the envelope from a decided one.</param>
/// <param name="ReasonCode">'question-disabled', 'invalid-input', 'rule-timeout', 'below-threshold' or 'no-provider-decided'.</param>
internal sealed record EscalatedEnvelope(bool Escalated, string ReasonCode)
{
    /// <summary>The recorded decision's id; only choose reports it (the hook questions' envelopes have none).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    /// <summary>The probabilities a provider sent before the call fell below threshold; only choose reports them.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, double>? Probabilities { get; init; }

    /// <summary>The envelope for an escalation.</summary>
    internal static EscalatedEnvelope For(EvaluationOutcome outcome) => new(Escalated: true, outcome.ReasonCode)
    {
        Id = (outcome as EscalatedOutcome)?.Id?.Value,
        Probabilities = (outcome as EscalatedOutcome)?.Probabilities,
    };
}
