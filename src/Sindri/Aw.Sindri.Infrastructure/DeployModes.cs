using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure;

/// <summary>Reads AW_SINDRI_DEPLOY, the one place that decides the mode (settings and the child environment both ask it).</summary>
internal static class DeployModes
{
    /// <summary>The mode a variable value selects.</summary>
    /// <param name="value">AW_SINDRI_DEPLOY, or null when unset.</param>
    /// <returns>Pod for "pod" in any case, otherwise the desktop.</returns>
    internal static DeployMode Of(string? value) =>
        ChildEnvironment.IsPod(value) ? DeployMode.Pod : DeployMode.Desktop;
}
