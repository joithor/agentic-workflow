namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the endpoint record's refusals.</summary>
internal static partial class Labels
{
    /// <summary>
    /// Why a control.json whose address isn't on this machine can't be followed (a hand-edited record, or one synced from
    /// another machine); it follows <see cref="RecordUnreadable"/>, and the record's path is its own line.
    /// </summary>
    /// <param name="authority">The recorded address (host and port).</param>
    /// <returns>The reason.</returns>
    internal static string NotOnThisMachine(string authority) =>
        $"its address {authority} isn't on this machine (the app follows only 127.0.0.1, ::1 or localhost)";
}
