namespace Aw.Sindri.Core.LensPacks;

/// <summary>One section a style pack asks of spec.md (line spec §5.1): its heading, whether it must be there and the most words it may hold.</summary>
/// <param name="Heading">The '## ' heading the section carries.</param>
/// <param name="Required">True when spec.md must hold the section.</param>
/// <param name="MaxWords">The most words the section's body may hold; null for no limit.</param>
public sealed record StyleSection(string Heading, bool Required, int? MaxWords);
