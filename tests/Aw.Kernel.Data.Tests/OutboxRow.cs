namespace Aw.Kernel.Data.Tests;

/// <summary>The delivery-state columns of one outbox_commands row, as stored.</summary>
public sealed record OutboxRow(long Attempts, string? LastErrorCode, string? LastErrorMessage, string? NextAttemptAt, string? DeadLetteredAt);
