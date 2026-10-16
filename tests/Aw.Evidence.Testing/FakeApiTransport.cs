using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>
/// A scripted IApiTransport: replies are chosen by '&lt;METHOD&gt; &lt;absolute URL&gt;' (an unknown one is a 404), a URL can be made to fail
/// as a connection error, and every call is recorded. Like the real transport it never follows a redirect.
/// </summary>
public sealed class FakeApiTransport : IApiTransport
{
    private readonly Dictionary<string, Func<HttpCall, HttpReply>> _routes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreachable = new(StringComparer.Ordinal);
    private readonly List<HttpCall> _calls = [];

    /// <summary>Every call, in order.</summary>
    public IReadOnlyList<HttpCall> Calls => _calls;

    /// <summary>Answers '&lt;method&gt; &lt;url&gt;' with <paramref name="reply"/>.</summary>
    /// <param name="method">The method ('GET').</param>
    /// <param name="url">The absolute URL.</param>
    /// <param name="reply">The reply.</param>
    /// <returns>This fake.</returns>
    public FakeApiTransport Reply(string method, string url, HttpReply reply) => Reply(method, url, _ => reply);

    /// <summary>Answers '&lt;method&gt; &lt;url&gt;' with whatever <paramref name="reply"/> makes of the call.</summary>
    /// <param name="method">The method.</param>
    /// <param name="url">The absolute URL.</param>
    /// <param name="reply">Makes the reply.</param>
    /// <returns>This fake.</returns>
    public FakeApiTransport Reply(string method, string url, Func<HttpCall, HttpReply> reply)
    {
        _routes[$"{method} {url}"] = reply;
        return this;
    }

    /// <summary>Makes every request to <paramref name="url"/> fail as if the connection was refused.</summary>
    /// <param name="url">The absolute URL.</param>
    /// <returns>This fake.</returns>
    public FakeApiTransport Unreachable(string url)
    {
        _ = _unreachable.Add(url);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<HttpReply>> SendAsync(HttpCall call, CancellationToken cancellationToken)
    {
        _calls.Add(call);
        if (_unreachable.Contains(call.Url))
        {
            return ValueTask.FromResult<Result<HttpReply>>(EvidenceErrors.RequestFailed.With($"{call.Url} did not answer"));
        }

        var reply = _routes.TryGetValue($"{ToName(call.Method)} {call.Url}", out var make) ? make(call) : new HttpReply(404, Location: null, "not found");
        return ValueTask.FromResult<Result<HttpReply>>(reply);
    }

    private static string ToName(HttpVerb method) => method.ToString().ToUpperInvariant();
}
