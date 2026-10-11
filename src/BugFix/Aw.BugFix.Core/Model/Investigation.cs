namespace Aw.BugFix.Core.Model;

/// <summary>The snapshot of the handoff taken at advance investigate; later steps use it, never the (mutable) file.</summary>
/// <param name="Handoff">The handoff file's full path.</param>
/// <param name="RootCause">The Root Cause text.</param>
/// <param name="Hypotheses">The hypotheses table.</param>
public sealed record Investigation(string Handoff, string RootCause, IReadOnlyList<Hypothesis> Hypotheses);
