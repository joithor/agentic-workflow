using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>One sandbox launch code.</summary>
/// <param name="Error">The catalogue entry.</param>
/// <param name="Kind">What the engine does with it.</param>
/// <param name="HasHttpClass">False for -019: it rides on session.ended and step.failed and is never an HTTP response (E1-S3).</param>
public sealed record SandboxCode(ErrorDefinition Error, LaunchCodeKind Kind, bool HasHttpClass);
