using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

internal sealed class FakeChild : IWrapChild
{
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<int> Exited => _exited.Task;

    public void Exit(int code) => _exited.TrySetResult(code);

    public bool Killed { get; private set; }

    public void Dispose()
    {
        if (_exited.TrySetResult(137))
        {
            Killed = true;   // like the real child: a dispose that finds it running kills it
        }
    }
}
