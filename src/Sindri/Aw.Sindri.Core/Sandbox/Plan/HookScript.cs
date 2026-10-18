namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>An installed hook script (config/hooks).</summary>
/// <param name="Name">The file name without '.sh'.</param>
/// <param name="AnswersPermissions">True when it prints a 'permissionDecision' of 'allow', which defeats 'dontAsk' (spike item 6).</param>
public sealed record HookScript(string Name, bool AnswersPermissions);
