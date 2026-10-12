using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The context checks, in doctor's order.</summary>
/// <param name="Checks">context:sources, then context:mcp-scope.</param>
public sealed record CheckContextResponse(IReadOnlyList<DoctorCheck> Checks);
