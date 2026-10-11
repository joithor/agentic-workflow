namespace Aw.Evidence.Core.Browser;

/// <summary>What a recorded session left behind; absolute paths.</summary>
/// <param name="Trace">The trace archive, when tracing ran.</param>
/// <param name="Videos">The session's videos.</param>
public sealed record SessionRecording(string? Trace, IReadOnlyList<string> Videos);
