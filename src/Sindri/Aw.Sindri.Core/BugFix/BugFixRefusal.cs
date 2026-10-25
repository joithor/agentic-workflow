namespace Aw.Sindri.Core.BugFix;

/// <summary>bugfix-state declined a step: a rule of the bug fix does not allow it. The state is untouched.</summary>
/// <param name="Code">bugfix-state's catalogue code ('AW-BUGFIX-010').</param>
/// <param name="Kind">The refusals a Step branches on.</param>
/// <param name="Message">bugfix-state's reason, in its words.</param>
public sealed record BugFixRefusal(string Code, RefusalKind Kind, string Message);
