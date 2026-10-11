using System.Text;

namespace Aw.Evidence.Infrastructure.Settings;

/// <summary>Reads one variable from a local, uncommitted env file of NAME=value lines (an optional export prefix, quotes stripped).</summary>
internal static class LocalEnvFile
{
    internal static string? Find(ReadOnlySpan<byte> content, string name)
    {
        string? found = null;
        foreach (var raw in Encoding.UTF8.GetString(content).Split('\n'))
        {
            var line = raw.Trim();
            line = line.StartsWith("export ", StringComparison.Ordinal) ? line["export ".Length..].TrimStart() : line;
            if (line.StartsWith(name + "=", StringComparison.Ordinal))
            {
                found = Unquote(line[(name.Length + 1)..].Trim());
            }
        }

        return string.IsNullOrEmpty(found) ? null : found;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] is '"' or '\'') && value[^1] == value[0] ? value[1..^1] : value;
}
