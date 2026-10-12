using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>Show the approved and live profile hashes and what changed (GET /sindri/profile, API spec §3.1).</summary>
public sealed record GetProfileRequest() : IQuery<GetProfileResponse>;
