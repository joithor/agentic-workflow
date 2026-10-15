using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>Onyx's 'GET /api/version' reply (onyx/server/manage/models.py VersionResponse).</summary>
/// <param name="BackendVersion">The release the backend runs: the image's ONYX_VERSION, the git tag for a release image.</param>
internal sealed record OnyxVersionReply([property: JsonPropertyName("backend_version")] string BackendVersion);
