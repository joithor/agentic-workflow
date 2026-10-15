namespace Aw.Sindri.Ui.Api;

/// <summary>
/// The fields of Sindri's ProblemDetails the app reads (API spec §3.6, D8): title is the SND code, detail the sentence
/// with its "(fix: …)", which the window shows as it is.
/// </summary>
/// <param name="Title">The SND code.</param>
/// <param name="Detail">What happened and the fix.</param>
internal sealed record ProblemView(string? Title, string? Detail);
