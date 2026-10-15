namespace Aw.Sindri.Core.Events;

/// <summary>A frame as a delta shows it.</summary>
/// <param name="State">The state.</param>
/// <param name="Entry">The entry number.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Child">The child the frame waits on.</param>
public sealed record FrameSummary(string State, int Entry, string? Run, string? Child);
