using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The running (kind, key) claims (API P6 unit C).</summary>
[Lane(TestLane.Unit)]
public sealed class JobRegistryTests
{
    private static readonly JobKind _kind = JobKind.From("RunService");
    private static readonly JobKey _key = JobKey.From("heartbeat");
    private static readonly JobId _other = JobId.From("JOB-00000000000000000000000000000002");

    [Fact]
    public void A_second_claim_of_a_kind_and_key_is_refused_and_names_the_holder()
    {
        var registry = new JobRegistry();
        Assert.True(registry.TryClaim(_kind, _key, A.FirstJobId, out _));

        var second = registry.TryClaim(_kind, _key, _other, out var running);

        Assert.False(second);
        Expect.Equal(A.FirstJobId, running);
        Expect.Equal(1, registry.Count());
    }

    [Fact]
    public void A_release_frees_the_claim_and_a_second_release_is_a_no_op()
    {
        var registry = new JobRegistry();
        _ = registry.TryClaim(_kind, _key, A.FirstJobId, out _);

        registry.Release(A.FirstJobId);
        registry.Release(A.FirstJobId);

        Assert.True(registry.TryClaim(_kind, _key, _other, out _));
        Expect.Equal(1, registry.Count());
    }

    [Fact]
    public void The_same_key_under_another_kind_is_a_different_claim()
    {
        var registry = new JobRegistry();
        _ = registry.TryClaim(_kind, _key, A.FirstJobId, out _);

        Assert.True(registry.TryClaim(JobKind.From("SyncSources"), _key, _other, out _));
    }
}
