using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A recorded drift (E-23).</summary>
/// <param name="Block">The block.</param>
/// <param name="NeedsReapproval">Whether re-approval is needed.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusDrift(
    BlockId Block,
    bool NeedsReapproval);
