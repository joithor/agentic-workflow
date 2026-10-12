namespace Aw.Sindri.Features.IssueNonce;

/// <summary>'POST /workflow/instances/{a}/nonce' (engine spec rev 8 §5.2, §6.3, class issue): a nonce for the next command on the instance.</summary>
/// <param name="Instance">The instance's address, or a bare item for the root of its latest attempt.</param>
public sealed record IssueNonceRequest(string Instance);
