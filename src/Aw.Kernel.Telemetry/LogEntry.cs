using System.Diagnostics;
using Aw.Kernel.Events;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry;

/// <summary>One log call, before scrubbing: what the logger was given and the context it ran in.</summary>
internal sealed record LogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>> Fields,
    EventContext? Context,
    Activity? Span);
