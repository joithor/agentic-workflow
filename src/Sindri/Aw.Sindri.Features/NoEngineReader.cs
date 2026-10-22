using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features;

/// <summary>The engine read side of a build that has no Temporal adapter registered: every read says the server doesn't answer (SND-ENGINE-001), so status counts no stuck instance and the engine routes answer 503.</summary>
internal sealed class NoEngineReader : IEngineReader
{
    public ValueTask<Result<EngineServer>> Server(CancellationToken cancellationToken) => Down<EngineServer>();

    public ValueTask<Result<EngineWorker>> Worker(CancellationToken cancellationToken) => Down<EngineWorker>();

    public ValueTask<Result<EngineMirror>> Mirror(CancellationToken cancellationToken) => Down<EngineMirror>();

    public ValueTask<Result<IReadOnlyList<EngineInstanceFacts>>> Instances(CancellationToken cancellationToken) => Down<IReadOnlyList<EngineInstanceFacts>>();

    public ValueTask<Result<EngineHistoryFacts>> History(string reference, int page, int pageSize, CancellationToken cancellationToken) => Down<EngineHistoryFacts>();

    private static ValueTask<Result<T>> Down<T>() => ValueTask.FromResult<Result<T>>(SindriErrors.EngineUnavailable.With("this build has no Temporal adapter"));
}
