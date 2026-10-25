using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>`POST /api/user/pats` answer (`CreatedTokenResponse`): the raw token is returned once; Onyx keeps only its hash.</summary>
/// <param name="Token">The token (`onyx_pat_…`).</param>
internal sealed record OnyxPatWire([property: JsonPropertyName("token")] string? Token);
