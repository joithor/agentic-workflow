using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Gate;

/// <summary>Chooses which gate scopes a pull request runs from the paths it changes.</summary>
public static class ScopePlanner
{
    /// <summary>Plans the scopes.</summary>
    /// <param name="changedPaths">Repository-relative paths the pull request changes.</param>
    /// <param name="gate">The gate configuration.</param>
    /// <returns>Scope names: the always scopes first, then every matching scope in configured order.</returns>
    public static IReadOnlyList<string> Plan(IReadOnlyList<string> changedPaths, GateConfig gate) => throw new NotSupportedException("ScopePlanner: make ScopePlannerTests pass");
}
