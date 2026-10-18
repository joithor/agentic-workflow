using System.Buffers;
using System.Text;

namespace Aw.Sindri.Core.Sessions;

/// <summary>The rules that turn a pane capture into preview lines (collector spec CA-9, CI-10), and the liveness check. Pure.</summary>
public static class PreviewRules
{
    /// <summary>The most lines a preview shows.</summary>
    public const int MaxLines = 40;

    /// <summary>The longest line, in UTF-16 code units.</summary>
    public const int MaxLineLength = 200;

    /// <summary>The most stdout bytes a capture keeps.</summary>
    public const int MaxBytes = 64 * 1024;

    /// <summary>The most parent hops the liveness check walks.</summary>
    public const int MaxHops = 64;

    /// <summary>Bidi marks, overrides and isolates, and the line and paragraph separators (collector spec CA-9 step 2).</summary>
    private static readonly SearchValues<char> _bidi = SearchValues.Create("\u061C\u200E\u200F\u2028\u2029\u202A\u202B\u202C\u202D\u202E\u2066\u2067\u2068\u2069");

    private const char Esc = '\u001b';
    private const char Bel = '\u0007';

    /// <summary>Steps 1 to 6 on the bytes tmux wrote.</summary>
    /// <param name="capture">The last <see cref="MaxBytes"/> plus one bytes (or fewer) of tmux's output; more is cut to the last <see cref="MaxBytes"/>.</param>
    /// <returns>The lines and whether anything was cut.</returns>
    public static PreviewText Of(ReadOnlySpan<byte> capture)
    {
        if (capture.Length <= MaxBytes)
        {
            return Of(Encoding.UTF8.GetString(capture), dropped: false);
        }

        var kept = capture[^MaxBytes..];
        var startsALine = capture[^(MaxBytes + 1)] == (byte)'\n';
        var newline = kept.IndexOf((byte)'\n');
        if (!startsALine)
        {
            kept = newline < 0 ? [] : kept[(newline + 1)..];   // the first line is a partial one: drop it whole, with any escape or UTF-8 sequence it holds
        }

        return Of(Encoding.UTF8.GetString(kept), dropped: true);
    }

    /// <summary>Steps 2 to 6 on decoded text.</summary>
    /// <param name="text">The decoded capture.</param>
    /// <param name="dropped">True when the 64 KiB cap dropped output before it.</param>
    /// <returns>The lines and whether anything was cut.</returns>
    public static PreviewText Of(string text, bool dropped)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = Strip(text).Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var cut = false;
        List<string> kept = [];
        foreach (var line in lines.Skip(Math.Max(0, lines.Count - MaxLines)))
        {
            var (shown, wasCut) = Cut(line);
            kept.Add(shown);
            cut |= wasCut;
        }

        return new PreviewText(kept, cut || dropped);
    }

    /// <summary>True when the session's process is <paramref name="panePid"/> or has it as an ancestor within <see cref="MaxHops"/> hops.</summary>
    /// <param name="pid">The session's process id.</param>
    /// <param name="startTicks">Its start ticks: the table's process at that pid must have them.</param>
    /// <param name="panePid">The pane's first process.</param>
    /// <param name="table">The process table.</param>
    /// <returns>True when live.</returns>
    public static bool InPane(int pid, long startTicks, int panePid, IReadOnlyList<ProcessEntry> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var byPid = new Dictionary<int, ProcessEntry>();
        foreach (var entry in table)
        {
            byPid[entry.Pid] = entry;
        }

        if (!byPid.TryGetValue(pid, out var current) || current.StartTicks != startTicks)
        {
            return false;
        }

        for (var hop = 0; hop <= MaxHops; hop++)
        {
            if (current.Pid == panePid)
            {
                return true;
            }

            if (!byPid.TryGetValue(current.ParentPid, out current))
            {
                return false;
            }
        }

        return false;
    }

    private static (string Shown, bool Cut) Cut(string line)
    {
        if (line.Length <= MaxLineLength)
        {
            return (line, false);
        }

        var length = char.IsHighSurrogate(line[MaxLineLength - 1]) && char.IsLowSurrogate(line[MaxLineLength]) ? MaxLineLength - 1 : MaxLineLength;
        return (line[..length], true);
    }

    /// <summary>Removes escape sequences and strings, control characters, C1 controls and bidi controls over the whole text; LF and tab stay.</summary>
    private static string Strip(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == Esc)
            {
                i = SkipEscape(text, i);
            }
            else if (!IsDropped(c))
            {
                _ = result.Append(c);
            }
        }

        return result.ToString();
    }

    /// <summary>The index of the last character of the escape sequence that starts at <paramref name="start"/>.</summary>
    private static int SkipEscape(string text, int start)
    {
        if (start + 1 >= text.Length)
        {
            return start;
        }

        var next = text[start + 1];
        return next switch
        {
            '[' => SkipCsi(text, start + 2),
            ']' => SkipString(text, start + 2, bellEnds: true),
            'P' or '_' or '^' or 'X' => SkipString(text, start + 2, bellEnds: false),
            _ => SkipOther(text, start + 1),
        };
    }

    /// <summary>A CSI sequence: parameter and intermediate bytes, then one final byte; anything else ends it without being eaten.</summary>
    private static int SkipCsi(string text, int from)
    {
        var i = from;
        while (i < text.Length && text[i] is >= ' ' and <= '?')
        {
            i++;
        }

        return i < text.Length && text[i] is >= '@' and <= '~' ? i : i - 1;
    }

    /// <summary>A string (OSC, DCS, APC, PM, SOS) up to ST (ESC backslash or U+009C), a bell for OSC, or the end of the text.</summary>
    private static int SkipString(string text, int from, bool bellEnds)
    {
        for (var i = from; i < text.Length; i++)
        {
            if (text[i] == '\u009C' || (bellEnds && text[i] == Bel))
            {
                return i;
            }

            if (text[i] == Esc && i + 1 < text.Length && text[i + 1] == '\\')
            {
                return i + 1;
            }
        }

        return text.Length - 1;
    }

    /// <summary>Any other escape: intermediate characters (0x20 to 0x2F) and one final character.</summary>
    private static int SkipOther(string text, int from)
    {
        var i = from;
        while (i < text.Length && text[i] is >= ' ' and <= '/')
        {
            i++;
        }

        return i < text.Length && text[i] is >= '0' and <= '~' ? i : i - 1;
    }

    private static bool IsDropped(char c) => c is not '\n' and not '\t' && (char.IsControl(c) || _bidi.Contains(c));   // IsControl: C0, DEL and C1
}
