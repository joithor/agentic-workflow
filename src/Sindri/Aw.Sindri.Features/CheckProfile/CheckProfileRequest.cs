using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.CheckProfile;

/// <summary>Compare the live profile with the approved one and record 'profile.checked' when the pair changed (API spec C8).</summary>
/// <param name="Grant">The lease grant the profile watch writes under.</param>
public sealed record CheckProfileRequest(LeaseGrant Grant);
