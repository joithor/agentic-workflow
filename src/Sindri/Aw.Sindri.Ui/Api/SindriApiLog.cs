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
}
