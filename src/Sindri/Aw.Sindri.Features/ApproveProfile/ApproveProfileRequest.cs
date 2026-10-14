using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ApproveProfile;

/// <summary>
/// Approve the live profile (design spec §8.7, §11.1). Without a hash it only shows the hash and what changed. With one,
/// the hash must be the live profile's and <see cref="Answer"/> must redeem an 'approve profile &lt;hash6&gt;' challenge
/// (engine spec §4.2, S1-D5): the CLI asks for one and sends what the human typed at the terminal, so every protected
/// action confirms the same way.
/// </summary>
/// <param name="Hash">The hash to approve, or null to see the live hash and the changes.</param>
/// <param name="Answer">The challenge and the text the human typed; null for a preview.</param>
public sealed record ApproveProfileRequest(ProfileHash? Hash, ChallengeAnswer? Answer = null) : ICommand<ApproveProfileResponse>;
