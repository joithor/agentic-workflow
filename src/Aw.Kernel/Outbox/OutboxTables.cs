namespace Aw.Kernel.Outbox;

/// <summary>Table names the outbox uses.</summary>
public static class OutboxTables
{
    /// <summary>
    /// The outbox table. Named 'outbox_commands', not 'outbox', so it never collides with the judge's
    /// existing outbox (spec §4e, B10).
    /// </summary>
    public const string Commands = "outbox_commands";
}
