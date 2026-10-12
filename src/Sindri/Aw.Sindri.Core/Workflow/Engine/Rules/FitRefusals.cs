using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>The SND-WF-003 refusals of the answer fit and the suspension rules: what is wrong, then '(fix: …)' naming what fits (spec §5.8, W27).</summary>
internal static class FitRefusals
{
    /// <summary>SND-WF-003 with its fix.</summary>
    /// <param name="problem">What is wrong.</param>
    /// <param name="fix">What to do.</param>
    /// <returns>The error.</returns>
    public static Error Refuse(string problem, string fix) => WorkflowErrors.TriggerNotAllowed.With($"{problem} (fix: {fix})");

    /// <summary>The status command, the fix when nothing else fits.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>'sindri workflow status {address}'.</returns>
    public static string Status(string address) => $"sindri workflow status {address}";

    /// <summary>An answer command.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="options">The answer's options.</param>
    /// <returns>'sindri workflow answer {address} {options}'.</returns>
    public static string Answer(string address, string options) => $"sindri workflow answer {address} {options}";
}
