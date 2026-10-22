using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Shipped;

/// <summary>A recipe file a manifest could not take in: it did not read, or the reader reported an error.</summary>
/// <param name="File">The file name.</param>
/// <param name="Finding">The first finding, as a line.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ManifestInvalid(string File, string Finding);
