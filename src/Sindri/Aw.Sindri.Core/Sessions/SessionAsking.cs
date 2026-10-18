namespace Aw.Sindri.Core.Sessions;

/// <summary>Why and since when a session waits on the human (collector spec CA-1).</summary>
/// <param name="Reason">Approval, input or auth.</param>
/// <param name="Tool">The tool an approval is for, or null.</param>
/// <param name="Since">When it started asking.</param>
public sealed record SessionAsking(AskingReason Reason, string? Tool, DateTimeOffset Since);
