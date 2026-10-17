using Aw.Sindri.Core.Sessions;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>What every <see cref="ISessionSnapshot"/> keeps: null before the first pass, then the latest published snapshot.</summary>
public abstract class SessionSnapshotContract
{
    /// <summary>A fresh snapshot store.</summary>
    /// <returns>The store.</returns>
    protected abstract ISessionSnapshot Create();

    [Fact]
    public void It_is_empty_until_the_first_pass() => Expect.True(Create().Current is null);

    [Fact]
    public void The_latest_published_snapshot_wins()
    {
        var subject = Create();
        var first = new SessionSnapshot([], [], [], DateTimeOffset.UnixEpoch);
        var second = first with { ObservedAt = DateTimeOffset.UnixEpoch.AddSeconds(2) };

        subject.Publish(first);
        subject.Publish(second);

        Expect.True(ReferenceEquals(second, subject.Current));
    }
}
