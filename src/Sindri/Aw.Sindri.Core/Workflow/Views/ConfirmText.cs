using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The confirmation sentences of destructive verbs (owner decision 2026-10-14; API spec section 3.8 'commands[].confirm').</summary>
public static class ConfirmText
{
    /// <summary>The sentence for cancelling an instance: what stops and what is cancelled with it.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="runningSteps">How many steps are running now (the open run).</param>
    /// <param name="openChildren">How many child instances are still open.</param>
    /// <returns>The sentence.</returns>
    public static string Cancel(string address, int runningSteps, int openChildren) =>
        string.Create(CultureInfo.InvariantCulture, $"Cancel {address}? {runningSteps} running steps stop; {openChildren} children are cancelled.");
}
