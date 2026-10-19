using System.Globalization;
using System.Net;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Onyx;

// The dedicated user's own tokens: list and revoke the old ones, make the new one.
internal sealed partial class OnyxSession
{
    public async ValueTask<Result<int>> RevokeTokens(string name, CancellationToken cancellationToken)
    {
        var listed = await Send(HttpMethod.Get, "/api/user/pats", content: null, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var tokens = listed.Value.Success ? AwJson.Deserialize<OnyxPatInfoWire[]>(Encoding.UTF8.GetBytes(listed.Value.Body), HttpOnyxProvisioning.Json) : null;
        if (tokens is not { IsSuccess: true, Value: { } found })
        {
            return SindriErrors.OnyxKeyFailed.With($"Onyx didn't list the tokens of Sindri's user (HTTP {Code(listed.Value.Status)}), so the old sindri-onyx ones can't be revoked; run `sindri onyx up` again");
        }

        var revoked = 0;
        foreach (var token in found.Where(t => string.Equals(t.Name, name, StringComparison.Ordinal)))
        {
            var gone = await Send(HttpMethod.Delete, $"/api/user/pats/{token.Id.ToString(CultureInfo.InvariantCulture)}", content: null, cancellationToken).ConfigureAwait(false);
            if (gone.IsError)
            {
                return gone.Error;
            }

            if (!gone.Value.Success && gone.Value.Status != HttpStatusCode.NotFound)
            {
                return SindriErrors.OnyxKeyFailed.With($"Onyx refused to revoke an old sindri-onyx token (HTTP {Code(gone.Value.Status)}); run `sindri onyx up` again");
            }

            revoked++;
        }

        return revoked;
    }

    public async ValueTask<Result<string>> CreateToken(string name, CancellationToken cancellationToken)
    {
        using var body = JsonBody(new OnyxPatRequestWire(name, ExpirationDays: null, Scopes: null));
        var sent = await Send(HttpMethod.Post, "/api/user/pats", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        if (sent.Value.Status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return NotAdmin(sent.Value.Status);
        }

        var made = sent.Value.Success ? AwJson.Deserialize<OnyxPatWire>(Encoding.UTF8.GetBytes(sent.Value.Body), HttpOnyxProvisioning.Json) : null;
        return made is { IsSuccess: true, Value.Token: { Length: > 0 } raw }
            ? raw
            : SindriErrors.OnyxKeyFailed.With($"Onyx didn't make the token (HTTP {Code(sent.Value.Status)}); run `sindri onyx up` again");
    }
}
