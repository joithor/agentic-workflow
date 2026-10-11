using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api;

/// <summary>The control plane's endpoint record (control.json): where it listens, its pid and its lease epoch.</summary>
/// <param name="Url">The API's base URL.</param>
/// <param name="Pid">The control plane's process id.</param>
/// <param name="Epoch">Its lease epoch; checked against /sindri/ping, so a stale record reads as not running.</param>
internal sealed record ControlRecord(Uri Url, int Pid, long Epoch)
{
    /// <summary>The record in <paramref name="bytes"/> when the app can follow it: it parses and names an absolute http(s) URL.</summary>
    /// <param name="bytes">The record's JSON.</param>
    /// <param name="json">The app's JSON options.</param>
    /// <returns>The record, or null.</returns>
    internal static ControlRecord? Usable(byte[] bytes, JsonSerializerOptions json)
    {
        var record = AwJson.Deserialize<ControlRecord>(bytes, json);
        return record.IsSuccess && record.Value.Url.IsAbsoluteUri
            && (string.Equals(record.Value.Url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || string.Equals(record.Value.Url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            ? record.Value
            : null;
    }
}
