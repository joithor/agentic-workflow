using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// What the validator sees of the registry (spec §3.2 'version', §4.3 RCP-CAP-003, W9), as data the caller resolved:
/// every registered (id, version, hash), and where each approved hash stands against this build. The validator does no
/// I/O, so the caller (the server, or the packet freeze) builds this from the recipe store and the approvals, with
/// PinRules.Check for each approved hash. A hash missing from <see cref="Approvals"/> was never approved.
/// </summary>
/// <param name="Registered">Every registered recipe's key.</param>
/// <param name="Approvals">Each approved hash's standing: holds, revoked or drifted.</param>
public sealed record RecipeRegistryView(IReadOnlyList<RegisteredVersion> Registered, IReadOnlyDictionary<RecipeHash, RegistryApproval> Approvals)
{
    /// <summary>A registry with nothing registered.</summary>
    public static RecipeRegistryView Empty { get; } = new([], new Dictionary<RecipeHash, RegistryApproval>());
}
