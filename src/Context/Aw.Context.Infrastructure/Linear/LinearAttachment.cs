namespace Aw.Context.Infrastructure.Linear;

/// <summary>An issue attachment (a pull request link, a Slack message, ...).</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Url">The Url Linear serves.</param>
/// <param name="Title">The Title Linear serves.</param>
/// <param name="SourceType">The SourceType Linear serves.</param>
internal sealed record LinearAttachment(string Id, string Url, string? Title = null, string? SourceType = null);
