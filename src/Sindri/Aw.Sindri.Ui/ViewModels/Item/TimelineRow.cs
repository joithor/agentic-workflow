namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>One timeline entry as the sheet shows it (UI spec §4.6): time, type, actor, and the API's one scrubbed line.</summary>
/// <param name="Sequence">The ledger sequence.</param>
/// <param name="Time">The local time ("11:55:00").</param>
/// <param name="Type">The event type.</param>
/// <param name="Actor">Who did it.</param>
/// <param name="Line">The line as served (plain text).</param>
internal sealed record TimelineRow(long Sequence, string Time, string Type, string Actor, string Line)
{
    /// <summary>The entry's accessible name: the whole line.</summary>
    public string Name => Time + " " + Type + " " + Actor + ": " + Line;
}
