using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'onyx.deferred' payload (setup-once spec §7.5): the owner chose "Not now" for Onyx, or took it back. Key 'onyx.deferred:&lt;commandId&gt;'.</summary>
/// <param name="CommandId">The 'DeferOnyx' command's id.</param>
/// <param name="Deferred">True to leave Onyx for later, false to offer it again.</param>
public sealed record OnyxDeferred(CommandId CommandId, bool Deferred);
