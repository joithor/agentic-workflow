namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One thing the person reads before answering a desk step (<c>current.desk.reviews</c>): the artifact a fact stands for.</summary>
/// <param name="Fact">The fact that holds it ("fact:design").</param>
/// <param name="Kind">design, plan, scope, packet, pr, questions or text; a kind this build doesn't know reads as the fact itself.</param>
/// <param name="Path">The file to open; null when none is served.</param>
/// <param name="Url">The link to open; null when none is served.</param>
internal sealed record DeskReview(string Fact, string Kind, string? Path = null, string? Url = null);
