namespace Aw.Sindri.Core.Checks;

/// <summary>One finding of a build or ship rule (line spec §7.2, §7.5): the rule that failed and where. A secret's finding names the file and line, never the match.</summary>
/// <param name="Rule">The rule id ('BLD-SCOPE-001').</param>
/// <param name="Message">What is wrong and, where it helps, the fix.</param>
/// <param name="Path">The file it concerns, or null.</param>
/// <param name="Line">The line in that file, or null.</param>
/// <param name="Detail">More for the reader (the tail of a failed command's output), already scrubbed; null for none.</param>
public sealed record RuleFinding(string Rule, string Message, string? Path = null, int? Line = null, string? Detail = null);
