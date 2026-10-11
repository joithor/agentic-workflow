namespace Aw.BugFix.Core.Model;

/// <summary>One ui-evidence run of the frozen script.</summary>
/// <param name="Script">The script's full path.</param>
/// <param name="RunDirectory">Where ui-evidence writes the run.</param>
/// <param name="Commit">The commit the served app was built from (ui-evidence's app build label).</param>
/// <param name="WorkingDirectory">The checkout to run in.</param>
/// <param name="Log">The process log file.</param>
public sealed record UiRunRequest(string Script, string RunDirectory, string Commit, string WorkingDirectory, string Log);
