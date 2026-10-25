using System.Collections.Concurrent;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows.Inputs;

/// <summary>What <see cref="ScopeProbeInput"/> saw: one line per fill, naming the scope's marker.</summary>
internal sealed class ScopeLog
{
    public ConcurrentQueue<Guid> Scopes { get; } = new();
}

/// <summary>A per-scope service: every DI scope gets its own id.</summary>
internal sealed class ScopeMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}

/// <summary>A Step input that reports the id of the DI scope it was resolved in.</summary>
/// <param name="marker">The scope's marker.</param>
/// <param name="log">Where the id goes.</param>
internal sealed class ScopeProbeInput(ScopeMarker marker, ScopeLog log) : IStepInput, IDescribedBlock
{
    public const string Id = "input.test.scope";

    public static BlockDescriptor Descriptor { get; } = TestBlocks.StepInput(Id);

    public ValueTask<Kernel.Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        log.Scopes.Enqueue(marker.Id);
        return ValueTask.FromResult<Kernel.Result<InjectionSections>>(new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = "scope" }));
    }
}

/// <summary>A Step input that reports the id of the DI scope it was resolved in.</summary>
/// <param name="marker">The scope's marker.</param>
/// <param name="log">Where the id goes.</param>
internal sealed class ScopeProbeTwoInput(ScopeMarker marker, ScopeLog log) : IStepInput, IDescribedBlock
{
    public const string Id = "input.test.scope2";

    public static BlockDescriptor Descriptor { get; } = TestBlocks.StepInput(Id);

    public ValueTask<Kernel.Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        log.Scopes.Enqueue(marker.Id);
        return ValueTask.FromResult<Kernel.Result<InjectionSections>>(new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = "scope" }));
    }
}

/// <summary>A Step input that reports the id of the DI scope it was resolved in.</summary>
/// <param name="marker">The scope's marker.</param>
/// <param name="log">Where the id goes.</param>
internal sealed class ScopeProbeThreeInput(ScopeMarker marker, ScopeLog log) : IStepInput, IDescribedBlock
{
    public const string Id = "input.test.scope3";

    public static BlockDescriptor Descriptor { get; } = TestBlocks.StepInput(Id);

    public ValueTask<Kernel.Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        log.Scopes.Enqueue(marker.Id);
        return ValueTask.FromResult<Kernel.Result<InjectionSections>>(new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = "scope" }));
    }
}
