using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>Why a previously approved recipe is not part of the hold (setup-once spec §7.2 'ownApproval.reason').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OwnApprovalReason>))]
public enum OwnApprovalReason
{
    /// <summary>Its new hash adds a capability.</summary>
    [JsonStringEnumMemberName("gains-capability")]
    GainsCapability = 1,

    /// <summary>A recipe it uses gained a capability.</summary>
    [JsonStringEnumMemberName("waits-for-child")]
    WaitsForChild = 2,
}
