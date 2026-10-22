namespace Aw.Sindri.Features.CheckPack;

/// <summary>What a pack check found.</summary>
/// <param name="Passed">Whether the pack has no finding.</param>
/// <param name="Id">The pack's id (its directory's name).</param>
/// <param name="Applies">'design' or 'review'; null when the pack didn't read.</param>
/// <param name="Lenses">How many lenses the pack holds; 0 when it didn't read.</param>
/// <param name="Hash">The pack's hash ('sha256:…'); null when the pack didn't pass.</param>
/// <param name="Pin">The profile line to approve ('workflow.packs.pins: lenses/&lt;id&gt;: sha256:…'); null when the pack didn't pass.</param>
/// <param name="Findings">What is wrong, one per problem.</param>
public sealed record CheckPackResponse(bool Passed, string Id, string? Applies, int Lenses, string? Hash, string? Pin, IReadOnlyList<PackFinding> Findings);

/// <summary>One problem in a pack.</summary>
/// <param name="Code">The error code (SND-PACK-003).</param>
/// <param name="Message">What is wrong and how to fix it.</param>
public sealed record PackFinding(string Code, string Message);
