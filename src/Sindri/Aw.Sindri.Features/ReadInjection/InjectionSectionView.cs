using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.ReadInjection;

/// <summary>One section's text with the metadata the plan recorded for it.</summary>
/// <param name="Section">The section name.</param>
/// <param name="Input">The input that filled it.</param>
/// <param name="Bytes">The UTF-8 size of the text.</param>
/// <param name="Sha256">The digest of the scrubbed text.</param>
/// <param name="Text">The scrubbed text.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InjectionSectionView(string Section, string Input, int Bytes, string Sha256, string Text);
