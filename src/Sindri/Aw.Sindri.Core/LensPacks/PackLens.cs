namespace Aw.Sindri.Core.LensPacks;

/// <summary>One lens of a pack: its id, title, prompt text and when it applies.</summary>
/// <param name="Id">The lens id, unique in the pack.</param>
/// <param name="Title">The lens's title.</param>
/// <param name="Prompt">The text of the lens's prompt file.</param>
/// <param name="When">When the lens applies.</param>
public sealed record PackLens(LensId Id, string Title, string Prompt, LensWhen When);
