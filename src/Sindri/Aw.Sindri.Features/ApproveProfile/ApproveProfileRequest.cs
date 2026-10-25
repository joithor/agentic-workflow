using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ApproveProfile;

/// <summary>
/// Approve the live profile (design spec §8.7, §11.1). Without a hash it only shows the hash and what changed. With one,
/// the hash must be the live profile's and the profile must validate; <see cref="Answer"/> is accepted and ignored (owner decision q93: no typed challenge).
/// </summary>
/// <param name="Hash">The hash to approve, or null to see the live hash and the changes.</param>
/// <param name="Answer">Accepted and ignored, so an older client still works.</param>
public sealed record ApproveProfileRequest(ProfileHash? Hash, ChallengeAnswer? Answer = null) : ICommand<ApproveProfileResponse>;
