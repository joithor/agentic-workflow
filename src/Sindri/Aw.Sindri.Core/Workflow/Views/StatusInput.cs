using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One input of the Step (E-23).</summary>
/// <param name="Key">The key.</param>
/// <param name="Name">The name.</param>
/// <param name="Hash">The hash.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusInput(
    string Key,
    string Name,
    string? Hash);
