namespace Aw.Sindri.Ui.Api;

/// <summary>The body of POST /sindri/requests (FileRequest).</summary>
/// <param name="Kind">The request's kind.</param>
/// <param name="ItemId">The item it concerns, or null.</param>
/// <param name="Text">What is asked.</param>
internal sealed record FileRequestRequest(string Kind, string? ItemId, string Text);
