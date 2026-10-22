namespace Aw.Sindri.Ui.Api;

/// <summary>The answer to a typed challenge.</summary>
/// <param name="Challenge">The challenge id.</param>
/// <param name="Typed">The typed text.</param>
internal sealed record ProfileAnswer(string Challenge, string Typed);
