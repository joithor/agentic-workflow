using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.HeldMap;

/// <summary>Who may hold, release and read the raw publish state: a service or a human; a session is refused (SND-CONTEXT-020), then the settings gate (001).</summary>
internal static class PublishAccess
{
    public static Error? Gate(ContextScope scope, string what) =>
        scope.Caller.Kind == ContextCallerKind.Session
            ? ContextErrors.SessionsDisabled.With($"{what} is a service job: a session can't (it reads the publish status)")
            : MapAccess.Gate(scope);
}
