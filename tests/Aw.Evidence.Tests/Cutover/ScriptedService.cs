using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>An IService that answers from a function and records the requests it got (a stand-in for a slice a host test doesn't exercise).</summary>
/// <typeparam name="TRequest">The request.</typeparam>
/// <typeparam name="TResponse">The response.</typeparam>
/// <param name="answer">What the service answers.</param>
internal sealed class ScriptedService<TRequest, TResponse>(Func<TRequest, Result<TResponse>> answer) : IService<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly List<TRequest> _requests = [];

    internal IReadOnlyList<TRequest> Requests => _requests;

    public ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return ValueTask.FromResult(answer(request));
    }
}
