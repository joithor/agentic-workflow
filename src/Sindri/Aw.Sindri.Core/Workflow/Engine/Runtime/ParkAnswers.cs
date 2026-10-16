using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The answer an open park or escalation takes (engine spec section 5.8, API spec section 3.8): shared by the status layout and the needs-you rows. Stub until API P11 U3 wires it.</summary>
public static class ParkAnswers
{
    /// <summary>The answer a suspension takes.</summary>
    /// <param name="shape">The suspension's kind, reason, park kind and escalation.</param>
    /// <param name="address">The instance's address.</param>
    /// <param name="grantTokens">The token amount a budget park's grant line offers.</param>
    /// <returns>The answer, or null when the suspension takes none (a drift, a revoked child or a takeover).</returns>
    public static StatusAnswer? For(ParkShape shape, InstanceAddress address, string grantTokens = "<n>") =>
        throw new NotSupportedException("ParkAnswers is built in API P11 U3.");
}
