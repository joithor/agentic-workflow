using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>
/// How soon a recipe's instances need a human's eye (UI screens rev 8 E-21): the order of the Home strip and of the
/// Workflows list's groups. A recipe that doesn't say is <see cref="Work"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Urgency>))]
public enum Urgency
{
    /// <summary>A live incident: first.</summary>
    [JsonStringEnumMemberName("incident")]
    Incident,

    /// <summary>Triage work: after incidents.</summary>
    [JsonStringEnumMemberName("triage")]
    Triage,

    /// <summary>Everything else; the default.</summary>
    [JsonStringEnumMemberName("work")]
    Work,
}
