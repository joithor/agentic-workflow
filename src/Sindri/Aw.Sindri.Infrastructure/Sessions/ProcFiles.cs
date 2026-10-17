using System.Globalization;
using System.Text;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>Reads the small /proc/&lt;pid&gt; files. A file that vanishes or isn't ours reads as null: a process can exit between two reads.</summary>
internal static class ProcFiles
{
    private const int ArgvZeroLimit = 4096;
    private const int EnvironLimit = 256 * 1024;

    /// <summary>The first field of the status file's 'Uid:' line (the real uid).</summary>
    internal static int? RealUid(string statusPath)
    {
        foreach (var line in Lines(statusPath))
        {
            if (line.StartsWith("Uid:", StringComparison.Ordinal))
            {
                var first = line["Uid:".Length..].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var uid) ? uid : null;
            }
        }

        return null;
    }

    /// <summary>The stat fields after the command name: 'state ppid pgrp session tty_nr … starttime' (field 3 on, so starttime is index 19), or null.</summary>
    internal static (string Comm, string[] Fields)? Stat(string statPath)
    {
        string text;
        try
        {
            text = File.ReadAllText(statPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        // "pid (comm) state ppid …": comm may hold spaces and parentheses, so it ends at the last ')'.
        var open = text.IndexOf('(', StringComparison.Ordinal);
        var close = text.LastIndexOf(')');
        return open < 0 || close <= open ? null : (text[(open + 1)..close], text[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>argv[0] only: the bytes up to the first NUL, read one byte at a time so nothing after it (a prompt) is ever read.</summary>
    internal static string? ArgvZero(string cmdlinePath)
    {
        try
        {
            using var handle = File.OpenHandle(cmdlinePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bytes = new List<byte>();
            Span<byte> one = stackalloc byte[1];
            while (bytes.Count < ArgvZeroLimit && RandomAccess.Read(handle, one, bytes.Count) == 1 && one[0] != 0)
            {
                bytes.Add(one[0]);
            }

            return bytes.Count == 0 ? null : Encoding.UTF8.GetString([.. bytes]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The environment's NUL-separated entries as name/value pairs. The caller filters them while it enumerates; nothing is kept here.</summary>
    internal static IEnumerable<KeyValuePair<string, string>> Environ(string environPath)
    {
        byte[] bytes;
        try
        {
            using var stream = File.OpenRead(environPath);
            var buffer = new byte[EnvironLimit];
            var read = stream.Read(buffer, 0, buffer.Length);
            bytes = buffer[..read];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var entry in Encoding.UTF8.GetString(bytes).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = entry.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0)
            {
                yield return new(entry[..eq], entry[(eq + 1)..]);
            }
        }
    }

    /// <summary>A symlink's target, or null.</summary>
    internal static string? Link(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static List<string> Lines(string path)
    {
        try
        {
            return [.. File.ReadLines(path)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
