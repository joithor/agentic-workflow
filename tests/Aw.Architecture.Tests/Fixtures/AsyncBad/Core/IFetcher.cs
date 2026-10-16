namespace Aw.Architecture.Tests.Fixtures.AsyncBad.Core;

public interface IFetcher
{
    public ValueTask<string> Fetch(string name);

    public Task Refresh(string name, CancellationToken cancellationToken);

    public ValueTask DisposeAsync();
}
