using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Host.Commands.Line;

/// <summary>Fences the stop view's text for a session caller (line spec LN-API2: "untrusted text fenced for sessions"). A human gets the view as it is.</summary>
internal static class DesignStopFencing
{
    /// <summary>The view with every piece of model- or person-written text inside the pack fence.</summary>
    internal static DesignStopView Fence(DesignStopView view) => throw new NotSupportedException();
}
