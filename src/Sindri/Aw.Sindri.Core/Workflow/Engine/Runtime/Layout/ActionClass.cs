using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>Whether an action is a desk or a protected action (E-23).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ActionClass>))]
public enum ActionClass
{
    /// <summary>A desk action.</summary>
    [JsonStringEnumMemberName("desk")]
    Desk,

    /// <summary>A protected action, taken at a terminal.</summary>
    [JsonStringEnumMemberName("protected")]
    Protected,
}
