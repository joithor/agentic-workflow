using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Ui.Testing;

/// <summary>One entry a <see cref="RecordingLogger"/> kept.</summary>
/// <param name="Level">Its level.</param>
/// <param name="EventName">Its event's name (the error code for the app's failures).</param>
/// <param name="Message">Its formatted text.</param>
/// <param name="Exception">The exception logged with it, if any.</param>
internal sealed record RecordedLog(LogLevel Level, string? EventName, string Message, Exception? Exception);
