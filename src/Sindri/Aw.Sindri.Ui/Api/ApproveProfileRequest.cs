namespace Aw.Sindri.Ui.Api;

/// <summary>POST /sindri/profile/approve's body: the live profile's full hash and the answer that redeems an 'approve profile &lt;hash6&gt;' challenge. Sent once.</summary>
/// <param name="Hash">The full hash to approve (the profile row's subject).</param>
/// <param name="Answer">The challenge and the text the person typed.</param>
internal sealed record ApproveProfileRequest(string Hash, ProfileAnswer Answer);
