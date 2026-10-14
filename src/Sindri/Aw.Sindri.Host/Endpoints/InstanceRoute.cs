using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The '{instanceId}' route segment (engine spec §6.3). Until instances get their opaque typed id ('WFI-…'), it is the
/// instance's address with each '/' escaped ('T-1%2Fstep%2F1'), or a bare item; the CLI escapes it, and this reads it back.
/// </summary>
internal static class InstanceRoute
{
    /// <summary>The address or bare item a route names, unescaped.</summary>
    /// <param name="instanceId">The route value.</param>
    /// <returns>The reference the slices resolve.</returns>
    internal static string Reference(string instanceId) => Uri.UnescapeDataString(instanceId);

    /// <summary>The address a route names; an invalid one throws Vogen's validation exception, which the Host maps to a 400 (SND-CLI-003).</summary>
    /// <param name="instanceId">The route value.</param>
    /// <returns>The address.</returns>
    internal static InstanceAddress Address(string instanceId) => InstanceAddress.From(Reference(instanceId));

    /// <summary>The route segment for <paramref name="reference"/>, as a client writes it.</summary>
    /// <param name="reference">An address or a bare item.</param>
    /// <returns>The escaped segment.</returns>
    internal static string Segment(string reference) => Uri.EscapeDataString(reference);
}
