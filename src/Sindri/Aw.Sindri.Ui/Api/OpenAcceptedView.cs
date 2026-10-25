namespace Aw.Sindri.Ui.Api;

/// <summary>POST /sindri/items/{itemId}/open's answer: the accepted command and the launch it started.</summary>
/// <param name="Accepted">The accepted command.</param>
/// <param name="Launch">The launch id (LCH-…); the app reads it for the outcome.</param>
/// <param name="Target">The agent id.</param>
internal sealed record OpenAcceptedView(CommandAcceptedView Accepted, string Launch, string Target);
