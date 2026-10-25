using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>What a pane capture becomes (collector spec CA-9, CI-10): escapes and controls stripped over the whole capture, then lines, the 40 x 200 bounds and the 64 KiB cap.</summary>
public sealed class PreviewRulesTests
{
    private static string[] Lines(string text, bool dropped = false) => [.. PreviewRules.Of(text, dropped).Lines];

    private static string Esc(string rest) => "\u001b" + rest;

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_keeps_the_last_40_lines()
    {
        var text = string.Join('\n', Enumerable.Range(1, 100).Select(static i => string.Create(CultureInfo.InvariantCulture, $"line {i}")));

        var lines = Lines(text);

        Assert.Equal(40, lines.Length);
        Assert.Equal(("line 61", "line 100"), (lines[0], lines[^1]));
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_cuts_each_line_at_200()
    {
        var result = PreviewRules.Of(new string('a', 250) + "\nshort", dropped: false);

        Assert.Equal([200, 5], result.Lines.Select(static l => l.Length));
        Assert.True(result.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_never_splits_a_surrogate_pair_when_it_cuts()
    {
        var line = new string('a', 199) + "\U0001F600" + "tail";   // the emoji is two code units at 199 and 200

        var cut = Lines(line)[0];

        Assert.Equal(199, cut.Length);
        Assert.DoesNotContain(cut, static c => char.IsSurrogate(c));
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_strips_escape_sequences_and_control_characters()
    {
        var text = Esc("[31;1mred") + Esc("[0m ") + Esc("]0;window title\u0007") + "ok" + "\u0001\u0007\u007f" + "\ttab" + Esc("c") + Esc("(B") + "!";

        Assert.Equal(["red ok\ttab!"], Lines(text));
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_strips_before_cutting()
    {
        var text = new string('a', 150) + Esc("[38;5;196m") + Esc("[48;2;1;2;3m") + new string('b', 40);   // 190 visible characters, 210 with the escapes

        var result = PreviewRules.Of(text, dropped: false);

        Assert.Equal(190, result.Lines[0].Length);
        Assert.False(result.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_strips_c1_and_bidi_controls()
    {
        var text = "a\u0085b\u009bc\u061Cd\u200Ee\u200Ff\u202Ag\u202Eh\u2066i\u2069j\u2028k\u2029l\u0080m\u009Fn";

        Assert.Equal(["abcdefghijklmn"], Lines(text));
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_strips_a_multi_line_osc_string()
    {
        var text = "before" + Esc("]0;title\nspanning\nlines") + Esc("\\") + "after\nnext" + "\n" + "x" + Esc("Pa\nb");   // a DCS with no terminator runs to the end

        Assert.Equal(["beforeafter", "next", "x"], Lines(text));
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_strips_every_string_kind_up_to_ST()
    {
        var st = Esc("\\");

        Assert.Equal(["ab"], Lines("a" + Esc("Pdcs\nx") + st + Esc("_apc") + st + Esc("^pm") + st + Esc("Xsos") + st + "b"));
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_marks_truncated_only_for_a_cut_line_or_the_cap()
    {
        Assert.True(PreviewRules.Of(new string('x', 201), dropped: false).Truncated);
        Assert.True(PreviewRules.Of("short", dropped: true).Truncated);
        Assert.False(PreviewRules.Of(new string('x', 200), dropped: false).Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_does_not_mark_truncated_for_the_normal_window() =>
        Assert.False(PreviewRules.Of(string.Join('\n', Enumerable.Range(1, 500).Select(static i => string.Create(CultureInfo.InvariantCulture, $"row {i}"))), dropped: false).Truncated);

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_splits_on_LF_and_drops_CR() =>
        Assert.Equal(["a", "bc", "d"], Lines("a\r\nb\rc\r\nd\r\n"));

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_drops_trailing_blank_lines_but_keeps_inner_ones() =>
        Assert.Equal(["a", "", "b"], Lines("a\n\nb\n\n  \n\t\n"));

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_of_nothing_is_no_lines()
    {
        Assert.Empty(Lines(string.Empty));
        Assert.Empty(Lines("\n\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_decodes_with_replacement_characters_and_never_throws() =>
        Assert.Equal(["a�b"], [.. PreviewRules.Of([(byte)'a', 0xFF, (byte)'b', (byte)'\n']).Lines]);

    [Fact, Trait("Lane", "unit")]
    public void Preview_keeps_the_last_64_KiB()
    {
        var all = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 30_000).Select(static i => string.Create(CultureInfo.InvariantCulture, $"row-{i:D5}\n"))));   // 300 KB

        var result = PreviewRules.Of(all);

        Assert.True(result.Truncated);
        Assert.Equal("row-29999", result.Lines[^1]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Preview_cuts_the_64_KiB_on_a_line_boundary()
    {
        var all = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 100).Select(static i => i.ToString("D3", CultureInfo.InvariantCulture) + new string('x', 1996) + "\n")));   // 2000-byte lines
        var tail = all[^(PreviewRules.MaxBytes + 1)..];   // what the reader keeps: the cap plus the byte before it

        var result = PreviewRules.Of(tail);

        Assert.StartsWith("068x", result.Lines[0], StringComparison.Ordinal);   // the cut line's tail is dropped, not shown as a line of its own
        Assert.Equal(32, result.Lines.Count);
        Assert.True(result.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public void Preview_keeps_the_first_line_when_the_cap_cuts_exactly_on_a_line_start()
    {
        var tail = new byte[PreviewRules.MaxBytes + 1];
        Array.Fill(tail, (byte)'x');
        tail[0] = (byte)'\n';
        Encoding.ASCII.GetBytes("begin").CopyTo(tail, 1);
        tail[^1] = (byte)'\n';

        var result = PreviewRules.Of(tail);

        Assert.StartsWith("beginx", result.Lines[0], StringComparison.Ordinal);
        Assert.True(result.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_a_capture_under_the_cap_is_not_marked_truncated() =>
        Assert.False(PreviewRules.Of("hello\nworld\n"u8).Truncated);

    private static ProcessEntry Proc(int pid, int parent, long ticks = 100) => new(pid, parent, "p", 0, ticks);

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_InPane_walks_parents_to_the_pane_process()
    {
        List<ProcessEntry> table = [Proc(1, 0), Proc(50, 1), Proc(60, 50), Proc(111, 60)];

        Assert.True(PreviewRules.InPane(111, 100, 50, table));
        Assert.True(PreviewRules.InPane(50, 100, 50, table));
        Assert.False(PreviewRules.InPane(111, 100, 999, table));
        Assert.False(PreviewRules.InPane(60, 100, 111, table));   // a child of the session is not an ancestor
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_InPane_needs_the_same_process()
    {
        List<ProcessEntry> table = [Proc(50, 1), Proc(111, 50, ticks: 777)];

        Assert.False(PreviewRules.InPane(111, 100, 50, table));   // the pid was recycled
        Assert.False(PreviewRules.InPane(222, 100, 50, table));   // the session's process is gone
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_InPane_gives_up_after_64_hops()
    {
        List<ProcessEntry> chain = [.. Enumerable.Range(0, 70).Select(static i => Proc(1000 + i, 999 + i))];   // 1069 -> 1068 -> ... -> 1000 -> 999 (not in the table)

        Assert.True(PreviewRules.InPane(1064, 100, 1000, chain));    // 64 hops
        Assert.False(PreviewRules.InPane(1065, 100, 1000, chain));   // 65
    }

    [Fact, Trait("Lane", "unit")]
    public void PreviewRules_InPane_stops_on_a_parent_loop()
    {
        List<ProcessEntry> loop = [Proc(10, 20), Proc(20, 10)];

        Assert.False(PreviewRules.InPane(10, 100, 99, loop));
    }
}
