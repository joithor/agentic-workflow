namespace Aw.Meta.Features.GateRerun;

/// <summary>One rerun: the command, the tree to run it in, the test, whether a filter that matches nothing is an error, and where the output goes.</summary>
/// <param name="Argv">The command.</param>
/// <param name="Tree">The directory to run it in.</param>
/// <param name="Test">The test name.</param>
/// <param name="FilterMustMatch">True on the gate tree, where the test exists.</param>
/// <param name="Log">The file the output is written to.</param>
internal sealed record RerunJob(IReadOnlyList<string> Argv, string Tree, string Test, bool FilterMustMatch, string Log);
