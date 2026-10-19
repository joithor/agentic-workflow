namespace Aw.Meta.Features.Watch;

/// <summary>The lanes reported CONTEXT HIGH so far, and how to keep them for a re-armed watch.</summary>
/// <param name="High">The lanes an earlier watch reported high.</param>
/// <param name="Keep">Keeps the lanes reported high whenever they change.</param>
internal sealed record ContextReported(IReadOnlySet<string> High, Func<IReadOnlySet<string>, ValueTask> Keep);
