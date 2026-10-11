using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>Which registry change woke the instance (spec §4.1, §5.4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecipeWakeKind>))]
public enum RecipeWakeKind
{
    /// <summary>The instance's recipe, or the child hash it pins, was revoked: it parks at once (a revoke replaces the stack).</summary>
    [JsonStringEnumMemberName("revoked")]
    Revoked = 1,

    /// <summary>At startup the build's descriptor hashes differ from the approval's pins: it parks with spec-conflict (drift).</summary>
    [JsonStringEnumMemberName("drifted")]
    Drifted = 2,

    /// <summary>A drift approval (or the parent's re-approval) resumes a drift or child-revoked park, never a rounds or budget park.</summary>
    [JsonStringEnumMemberName("approved")]
    Approved = 3,
}
