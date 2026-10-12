using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop head's log lines (codes from the app's catalogue, Aw.Sindri.Ui's Api/SindriUiErrors.cs).</summary>
internal static partial class DesktopLog
{
    /// <summary>SND-UI-004 before the app starts: claiming the instance or handing over threw.</summary>
    /// <param name="logger">The head's log.</param>
    /// <param name="exceptionType">The exception's type name (never its message: it can carry the home's path).</param>
    [LoggerMessage(EventId = 4, EventName = "SND-UI-004", Level = LogLevel.Error, Message = "SND-UI-004 the Sindri app failed before it started ({ExceptionType}) and exits with code 2")]
    internal static partial void FailedBeforeStart(ILogger logger, string exceptionType);
}
