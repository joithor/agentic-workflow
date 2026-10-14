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

    /// <summary>Why the platform refused to read control.json: no permission (the app's words, never the platform's message, which names the path).</summary>
    internal const string RecordNoPermission = "this user has no permission to read it";

    /// <summary>Why control.json can't be read when it isn't a file (a directory in its place).</summary>
    internal const string RecordNotAFile = "it isn't a file (something else, such as a directory, is in its place)";

    /// <summary>Why the platform couldn't read control.json otherwise (an I/O error it doesn't name further).</summary>
    internal const string RecordIoError = "the platform couldn't read it";
}
