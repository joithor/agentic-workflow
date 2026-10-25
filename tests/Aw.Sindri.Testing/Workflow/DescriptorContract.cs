using System.Reflection;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// What every shipped block's descriptor does (spec §2 "Descriptor hash"): it hashes to the pinned value, its kind
/// matches its id prefix, the real composition registers it exactly once, and a descriptor-only block has no runtime.
/// A block with a runtime derives from <see cref="BlockContract"/> instead.
/// </summary>
public abstract class DescriptorContract
{
    private static readonly Type[] _runtimeKinds = [typeof(IGate), typeof(IStep), typeof(IBlockStep), typeof(IEffectKind), typeof(IStepInput), typeof(IEvidenceProvider)];

    /// <summary>The descriptor under test.</summary>
    protected abstract BlockDescriptor Descriptor { get; }

    /// <summary>The descriptor's pinned RFC 8785 sha256 ('sha256:…'): a change is a new block version, never a silent edit.</summary>
    protected abstract string ExpectedHash { get; }

    /// <summary>The serializer options the hash is taken with (the host's).</summary>
    protected abstract JsonSerializerOptions Json { get; }

    /// <summary>The real composition, as the host registers it.</summary>
    /// <returns>The provider.</returns>
    protected abstract IServiceProvider Compose();

    /// <summary>Whether the block has a runtime; false here, true in <see cref="BlockContract"/>.</summary>
    protected virtual bool HasRuntime => false;

    /// <summary>The descriptor hashes to the pinned value.</summary>
    [Fact]
    public void Descriptor_hash_is_pinned()
    {
        var provider = Compose();
        using var lifetime = provider as IDisposable;
        var composed = provider.GetRequiredService<BlockCatalog>().Find(Descriptor.Id);

        Expect.Equal(ExpectedHash, PinRules.Hash(composed!, Json).Value);
    }

    /// <summary>The kind matches the id prefix ('gate.', 'step.', 'effect.', 'input.', 'evidence.').</summary>
    [Fact]
    public void Kind_matches_the_id_prefix() => Expect.Equal(BlockKinds.Of(Descriptor.Id), Descriptor.Kind);

    /// <summary>The composed catalog holds exactly one descriptor with this id.</summary>
    [Fact]
    public void Registered_exactly_once()
    {
        var provider = Compose();
        using var lifetime = provider as IDisposable;

        Expect.Equal(1, provider.GetRequiredService<BlockCatalog>().Blocks.Count(b => b.Id == Descriptor.Id));
    }

    /// <summary>A descriptor-only block has no runtime in the composition; one with a runtime has one (see <see cref="BlockContract"/>).</summary>
    [Fact]
    public void A_runtime_is_registered_only_when_the_block_has_one()
    {
        var provider = Compose();
        using var lifetime = provider as IDisposable;
        var carriers = RuntimeTypes(provider).Count(t => IsDescribedAs(t, Descriptor));

        Expect.Equal(HasRuntime ? 1 : 0, carriers);
    }

    private static List<Type> RuntimeTypes(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return [.. _runtimeKinds.SelectMany(scope.ServiceProvider.GetServices).OfType<object>().Select(o => o.GetType()).Distinct()];
    }

    private static bool IsDescribedAs(Type type, BlockDescriptor descriptor) =>
        (type.GetProperty(nameof(IDescribedBlock.Descriptor), BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is BlockDescriptor d && d.Id == descriptor.Id)
        || (type.GetProperty(nameof(IBlockStepId.Id), BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is BlockId id && id == descriptor.Id);   // a runtime registered by id (AddBlockRuntime) carries no descriptor
}
