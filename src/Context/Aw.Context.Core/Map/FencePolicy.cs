namespace Aw.Context.Core.Map;

/// <summary>
/// Whether untrusted map text is wrapped in the fence on its way out. Provisional owner default (2026-10-14 night, spec §7): a
/// human reading through the CLI or the app gets the clean text beside its trust field (the client fences it where a model
/// could read it); a session, an MCP call and an in-process caller keep the fence.
/// </summary>
public enum FencePolicy
{
    /// <summary>Untrusted text is wrapped in the untrusted fence.</summary>
    Fenced,

    /// <summary>Untrusted text is written clean; its trust field says it is untrusted.</summary>
    Clean,
}
