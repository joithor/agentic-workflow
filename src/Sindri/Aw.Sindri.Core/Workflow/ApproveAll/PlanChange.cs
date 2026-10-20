using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>What changed in a plan row since the recipe was last approved (setup-once spec §7.2 'change').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlanChange>))]
public enum PlanChange
{
    /// <summary>Never approved.</summary>
    [JsonStringEnumMemberName("new")]
    New = 1,

    /// <summary>Approved before; its own content changed.</summary>
    [JsonStringEnumMemberName("updated")]
    Updated = 2,

    /// <summary>Approved before and unchanged itself, but a recipe it uses changed.</summary>
    [JsonStringEnumMemberName("second-look")]
    SecondLook = 3,
}
