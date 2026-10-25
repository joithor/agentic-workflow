using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The stage a check runs at (scoping spec §8.2): after every draft and apply, or in the evidence step.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScopeStage>))]
public enum ScopeStage
{
    /// <summary>'step.scope.check': the draft-stage rules.</summary>
    [JsonStringEnumMemberName("draft")]
    Draft = 1,

    /// <summary>'step.scope.evidence': every rule, the draft ones included.</summary>
    [JsonStringEnumMemberName("evidence")]
    Evidence = 2,
}
