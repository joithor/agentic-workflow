using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Commands;

/// <summary>A query's only decorator besides validation (API spec §2.2): a 'sindri.query &lt;name&gt;' span around it.</summary>
/// <param name="inner">Validation and the handler.</param>
/// <typeparam name="TRequest">The query record.</typeparam>
/// <typeparam name="TResponse">The view it answers.</typeparam>
internal sealed class QuerySpan<TRequest, TResponse>(IService<TRequest, TResponse> inner) : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    private static readonly string _name = "sindri.query " + (typeof(TRequest).Name.EndsWith("Request", StringComparison.Ordinal)
        ? typeof(TRequest).Name[..^"Request".Length]
        : typeof(TRequest).Name);

    /// <summary>The service it wraps (for the chain-order test).</summary>
    internal IService<TRequest, TResponse> Next => Inner;

    public override async ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        using var span = CommandTelemetry.Source.StartActivity(_name);
        return await Inner.Handle(request, cancellationToken).ConfigureAwait(false);
    }
}
