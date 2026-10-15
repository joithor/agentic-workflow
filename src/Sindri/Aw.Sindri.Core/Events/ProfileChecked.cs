using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'profile.checked' payload (API spec C8): written when the (approved, live) pair changes.</summary>
/// <param name="CommandId">The check's command id, minted by its caller context.</param>
/// <param name="Approved">The approved hash, or null when none was approved.</param>
/// <param name="Live">The live profile's hash, or null when it can't be loaded.</param>
/// <param name="Drifted">True when a profile is approved and the live one differs from it.</param>
/// <param name="ChangedPaths">The key paths that differ, when both load.</param>
/// <param name="ApprovedServices">The services the approved profile names.</param>
public sealed record ProfileChecked(
    CommandId CommandId,
    ProfileHash? Approved,
    ProfileHash? Live,
    bool Drifted,
    IReadOnlyList<string> ChangedPaths,
    IReadOnlyList<ServiceName> ApprovedServices);
