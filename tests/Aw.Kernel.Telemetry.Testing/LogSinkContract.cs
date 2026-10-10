using System.Globalization;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Kernel.Telemetry.Testing;

/// <summary>
/// The contract every <see cref="ILogSink"/> honours, run against the in-memory fake and each real sink (spec §4b.5,
/// csharp.md Seams). A derived class binds a sink and carries its lane: unit for in-memory writers, integration for a
/// real file.
/// </summary>
public abstract class LogSinkContract
{
    private const int Writers = 8;
    private const int LinesPerWriter = 200;

    /// <summary>A new, empty sink.</summary>
    /// <returns>The sink.</returns>
    protected abstract ILogSink CreateSink();

    /// <summary>Everything <paramref name="sink"/> has delivered to its destination, read back without the sink's code.</summary>
    /// <param name="sink">A sink from <see cref="CreateSink"/>, already flushed.</param>
    /// <returns>The destination's text.</returns>
    protected abstract string Delivered(ILogSink sink);

    /// <summary>Lines arrive in order, each exactly once and ending with '\n'.</summary>
    [Fact]
    public void Lines_arrive_in_order_each_ending_with_a_newline()
    {
        var sink = CreateSink();
        string[] lines = ["{\"n\":1}", "{\"n\":2}", "{\"n\":3}"];
        foreach (var line in lines)
        {
            sink.WriteLine(line);
        }

        sink.Flush();
        Expect(nameof(Lines_arrive_in_order_each_ending_with_a_newline), "{\"n\":1}\n{\"n\":2}\n{\"n\":3}\n", Delivered(sink));
    }

    /// <summary>Text outside ASCII is delivered unchanged.</summary>
    [Fact]
    public void Text_outside_ascii_is_delivered_unchanged()
    {
        var sink = CreateSink();
        const string line = "{\"m\":\"café ✓ 日本\"}";
        sink.WriteLine(line);
        sink.Flush();
        Expect(nameof(Text_outside_ascii_is_delivered_unchanged), line + "\n", Delivered(sink));
    }

    /// <summary>A line with a line break is refused, so one record can never become two.</summary>
    [Fact]
    public void A_line_with_a_line_break_is_refused()
    {
        var sink = CreateSink();
        foreach (var bad in new[] { "{\"a\":1}\n{\"b\":2}", "{\"a\":1}\r" })
        {
            try
            {
                sink.WriteLine(bad);
            }
            catch (ArgumentException)
            {
                continue;
            }

            throw new ContractViolationException(
                $"{nameof(A_line_with_a_line_break_is_refused)}: WriteLine accepted a line containing a line break.");
        }

        sink.Flush();
        Expect(nameof(A_line_with_a_line_break_is_refused), string.Empty, Delivered(sink));
    }

    /// <summary>Concurrent writers never interleave inside a line, and no line is lost.</summary>
    [Fact]
    public void Concurrent_writers_never_interleave_or_lose_lines()
    {
        var sink = CreateSink();
        _ = Parallel.For(0, Writers, w =>
        {
            for (var i = 0; i < LinesPerWriter; i++)
            {
                sink.WriteLine(Numbered(w, i));
            }
        });
        sink.Flush();

        var got = Delivered(sink).Split('\n');
        var expected = Enumerable.Range(0, Writers)
            .SelectMany(w => Enumerable.Range(0, LinesPerWriter).Select(i => Numbered(w, i)))
            .Order(StringComparer.Ordinal)
            .ToList();
        var lines = got[..^1].Order(StringComparer.Ordinal).ToList();
        if (got[^1].Length != 0 || !lines.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new ContractViolationException(
                string.Create(CultureInfo.InvariantCulture, $"{nameof(Concurrent_writers_never_interleave_or_lose_lines)}: expected {expected.Count} intact lines, got {lines.Count} (or a broken line)."));
        }
    }

    /// <summary>Flushing an empty sink delivers nothing and doesn't throw.</summary>
    [Fact]
    public void Flushing_an_empty_sink_delivers_nothing()
    {
        var sink = CreateSink();
        sink.Flush();
        Expect(nameof(Flushing_an_empty_sink_delivers_nothing), string.Empty, Delivered(sink));
    }

    private static string Numbered(int writer, int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{{\"w\":{writer},\"i\":{index},\"pad\":\"{new string('x', 64)}\"}}");

    private static void Expect(string test, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new ContractViolationException($"{test}: expected {Show(expected)}, delivered {Show(actual)}.");
        }
    }

    private static string Show(string text) => text.Length > 200 ? string.Create(CultureInfo.InvariantCulture, $"{text.Length} chars") : $"'{text.Replace("\n", "\\n", StringComparison.Ordinal)}'";
}
