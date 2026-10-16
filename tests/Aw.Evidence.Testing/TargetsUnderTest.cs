using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Testing;

/// <summary>A targets port arranged by an <see cref="EvidenceTargetsContract"/> subclass.</summary>
/// <param name="Port">The port under test.</param>
/// <param name="Repo">The repository (absolute).</param>
/// <param name="ConfigPath">The --config file arranged, or null when none was.</param>
public sealed record TargetsUnderTest(IEvidenceTargets Port, string Repo, string? ConfigPath);
