namespace Aw.Sindri.Ui.Api;

/// <summary>An API refusal (ProblemDetails, API spec §9): 'title' is the SND code and 'detail' the message with its fix, shown as is.</summary>
/// <param name="Title">The SND code.</param>
/// <param name="Status">The HTTP status.</param>
/// <param name="Detail">The message, with its '(fix: …)'.</param>
internal sealed record ProblemView(string Title, int Status, string Detail);
