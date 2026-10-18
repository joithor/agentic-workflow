using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The per-run egress.log: a count per host, port and verdict, never a URL (sandbox spec §3.2).</summary>
/// <param name="path">The file.</param>
internal sealed class EgressLog(string path)
{
    private readonly ConcurrentDictionary<(string Host, int Port, EgressVerdict Verdict), int> _counts = new();

    /// <summary>Counts one request.</summary>
    /// <param name="host">The host.</param>
    /// <param name="port">The port.</param>
    /// <param name="verdict">The verdict.</param>
    public void Record(string host, int port, EgressVerdict verdict) =>
        _counts.AddOrUpdate((Clean(host), port, verdict), 1, static (_, n) => n + 1);

    /// <summary>The count for one entry.</summary>
    /// <param name="host">The host.</param>
    /// <param name="port">The port.</param>
    /// <param name="verdict">The verdict.</param>
    /// <returns>The count.</returns>
    public int Count(string host, int port, EgressVerdict verdict) => _counts.GetValueOrDefault((Clean(host), port, verdict));

    /// <summary>Writes the file (temp file and rename): one line per entry, host, port, verdict and count separated by tabs.</summary>
    public void Flush()
    {
        var text = new StringBuilder();
        foreach (var ((host, port, verdict), count) in _counts.OrderBy(e => e.Key.Host, StringComparer.Ordinal).ThenBy(e => e.Key.Port).ThenBy(e => e.Key.Verdict))
        {
            _ = text.Append(host).Append('\t').Append(port.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(verdict.ToString().ToLowerInvariant()).Append('\t').Append(count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        var temp = path + ".tmp";
        File.WriteAllText(temp, text.ToString());
        File.Move(temp, path, overwrite: true);
    }

    private static string Clean(string host) => host.Length > 253 ? host[..253] : host.Replace('\t', '_').Replace('\n', '_').Replace('\r', '_').ToLowerInvariant();
}
