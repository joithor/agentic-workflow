using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// Doctor's 'context:onyx' version check (spec §8.8): 'GET &lt;url&gt;/api/version' with no Authorization header (the
/// route is public in Onyx v4.9.0, onyx/server/auth_check.py), and the reported 'backend_version' compared with the
/// pinned release, sent through <see cref="OnyxRequests"/> like every Onyx call. No answer, an HTTP error or a reply that isn't Onyx's is Unavailable with SND-CONTEXT-010; another
/// version is Unavailable with SND-CONTEXT-055. The caller's own cancellation is thrown, not turned into a health.
/// </summary>
/// <param name="http">The client; production passes <see cref="OnyxHttp.Client"/> (no proxy, no redirects).</param>
internal sealed class OnyxVersionProbe(HttpClient http)
{
    private const string VersionPath = "api/version";

    /// <summary>Asks Onyx for its version.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="deadline">How long Onyx gets to answer.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>Ok, or Unavailable with SND-CONTEXT-010 (no usable answer) or SND-CONTEXT-055 (another version).</returns>
    public async ValueTask<SourceHealth> Check(OnyxSettings settings, TimeSpan deadline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(deadline);
        try
        {
            var reply = await OnyxRequests.Send(http, settings.Url, OnyxCall.Get("/" + VersionPath), token: null, timeout.Token).ConfigureAwait(false);
            return (int)reply.Status is >= 200 and < 300
                ? Compare(settings, reply.Body)
                : Unavailable(settings, string.Create(CultureInfo.InvariantCulture, $"answered HTTP {(int)reply.Status} on /{VersionPath}; is this Onyx?"));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(settings, string.Create(CultureInfo.InvariantCulture, $"didn't answer /{VersionPath} within {deadline.TotalSeconds:0.###} s"));
        }
        catch (HttpRequestException e)
        {
            return Unavailable(settings, $"isn't reachable ({e.HttpRequestError}); start Onyx or check the url");
        }
    }

    private static SourceHealth Compare(OnyxSettings settings, byte[] body)
    {
        var reply = AwJson.Deserialize<OnyxVersionReply>(body, OnyxWire.Options);
        if (reply.IsError)
        {
            return Unavailable(settings, $"answered /{VersionPath} without a backend_version; is this Onyx?");
        }

        var check = settings.CheckVersion(reply.Value.BackendVersion);
        return check.IsError
            ? new SourceHealth(SourceState.Unavailable, check.Error.Message, check.Error.Code)
            : new SourceHealth(SourceState.Ok, $"Onyx {reply.Value.BackendVersion} answers on {settings.Address} (pinned {settings.Version})", Code: null);
    }

    private static SourceHealth Unavailable(OnyxSettings settings, string what) =>
        new(SourceState.Unavailable, $"Onyx at {settings.Address} {what}", ContextErrors.SourceUnavailable.Code);
}
