namespace Aw.Architecture.Tests.Fixtures.Good.Core;

public interface IItemFeed
{
    public ValueTask<Item?> Next(CancellationToken cancellationToken);

    public ValueTask DisposeAsync();
}
