using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry.Tests;

/// <summary>
/// The two ways code logs: source-generated [LoggerMessage] methods (structured fields, the production idiom) and
/// <see cref="Emit"/>, which hands the logger finished text through ILogger.Log (no template), for messages built at
/// run time such as a secret pasted into the text.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 7, EventName = "ItemStarted", Level = LogLevel.Information, Message = "Started {Item} in {Ms} ms")]
    public static partial void ItemStarted(ILogger logger, string item, int ms);

    [LoggerMessage(Level = LogLevel.Information, Message = "env {Pair} token {Token}")]
    public static partial void EnvAndToken(ILogger logger, string pair, string token);

    [LoggerMessage(Level = LogLevel.Information, Message = "login {Password}")]
    public static partial void Login(ILogger logger, string password);

    [LoggerMessage(Level = LogLevel.Error, Message = "failed")]
    public static partial void Failed(ILogger logger, Exception exception);

    public static void Emit(this ILogger logger, LogLevel level, string text, EventId eventId = default) =>
        logger.Log(level, eventId, text, exception: null, static (state, _) => state);
}
