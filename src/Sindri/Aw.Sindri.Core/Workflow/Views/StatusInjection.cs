using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>An injection line of a status: one section the open run's Step inputs filled, by metadata only (never the text).</summary>
/// <param name="Section">The section name.</param>
/// <param name="Input">The input that filled it.</param>
/// <param name="Bytes">The UTF-8 size of the scrubbed text.</param>
/// <param name="Hash">The digest of the scrubbed text, as 'sha256:' and 64 hex digits.</param>
/// <param name="Replaced">The earlier input whose section of the same name this one replaced; null when none.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusInjection(
    string Section,
    string Input,
    int Bytes,
    string Hash,
    string? Replaced = null);
