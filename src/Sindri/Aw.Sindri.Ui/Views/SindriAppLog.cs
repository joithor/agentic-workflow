using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Ui.Views;

/// <summary>The app's log lines (codes from the app's catalogue, Api/SindriUiErrors.cs).</summary>
internal static partial class SindriAppLog
{
    /// <summary>SND-UI-004: the app failed and exits with <see cref="SindriApp.FailedExitCode"/>.</summary>
    /// <param name="logger">The app's log.</param>
    /// <param name="exceptionType">The exception's type name (never its message).</param>
    [LoggerMessage(EventId = 4, EventName = "SND-UI-004", Level = LogLevel.Error, Message = "SND-UI-004 the Sindri app failed ({ExceptionType}) and exits with code 2")]
    internal static partial void Failed(ILogger logger, string exceptionType);
}
