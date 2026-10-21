using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>Who may hold, release and read the raw publish state: a service or a human; a session is refused (SND-CONTEXT-020), then the settings gate (001).</summary>
public static class PublishAccess
{
    /// <summary>The check for a service-only publish call.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="what">What the caller tried, for the message.</param>
    /// <returns>Null when the caller may, otherwise SND-CONTEXT-020 for a session or the settings gate's error.</returns>
    public static Error? Gate(ContextScope scope, string what) =>
        scope.Caller.Kind == ContextCallerKind.Session
            ? ContextErrors.SessionsDisabled.With($"{what} is a service job: a session can't (it reads the publish status)")
            : MapAccess.Gate(scope);
}
