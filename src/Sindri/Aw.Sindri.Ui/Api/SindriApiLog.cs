using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Ui.Api;

/// <summary>The API client's log lines (codes from the app's catalogue, <see cref="SindriUiErrors"/>).</summary>
internal static partial class SindriApiLog
{
    /// <summary>
    /// SND-UI-003: a route answered with a body this build can't read. The parser's detail (the record, the JSON path,
    /// the byte position) goes here for whoever runs sindri doctor; the window shows only the plain sentence.
    /// </summary>
    /// <param name="logger">The app's log.</param>
    /// <param name="path">The route that answered.</param>
    /// <param name="detail">The parser's message.</param>
    [LoggerMessage(EventId = 3, EventName = "SND-UI-003", Level = LogLevel.Warning, Message = "SND-UI-003 {Path} answered with a body this build can't read: {Detail}")]
    internal static partial void Unreadable(ILogger logger, string path, string detail);

    /// <summary>
    /// The human token file can't be read (no permission, a directory in its place): the call goes without the token and
    /// Sindri refuses it with SND-AUTH-001, whose detail the window shows. The line names the file and the exception's
    /// type only, never the token.
    /// </summary>
    /// <param name="logger">The app's log.</param>
    /// <param name="location">Where the token is read from.</param>
    /// <param name="type">The exception's type name.</param>
    [LoggerMessage(EventId = 5, EventName = "HumanTokenUnreadable", Level = LogLevel.Warning, Message = "{Location} can't be read ({Type}): the call goes without X-Sindri-Token, so Sindri refuses it (SND-AUTH-001)")]
    internal static partial void TokenUnreadable(ILogger logger, string location, string type);
}
