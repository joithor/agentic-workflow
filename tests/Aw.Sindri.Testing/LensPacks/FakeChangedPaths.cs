using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Testing.LensPacks;

/// <summary>An <see cref="IChangedPaths"/> in memory: the paths a test gave for a checkout and the two refs, none for any other pair. Held to <see cref="ChangedPathsContract"/>.</summary>
public sealed class FakeChangedPaths : IChangedPaths
{
    private readonly Dictionary<(string Checkout, string Base, string Head), IReadOnlyList<string>> _changes = [];

    /// <summary>The calls made, as 'checkout base...head'.</summary>
    public IList<string> Calls { get; } = [];

    /// <summary>Holds the paths between two refs of a checkout.</summary>
    /// <param name="checkout">The checkout.</param>
    /// <param name="baseRef">The base.</param>
    /// <param name="head">The head.</param>
    /// <param name="paths">The paths that differ.</param>
    /// <returns>The same instance.</returns>
    public FakeChangedPaths With(string checkout, string baseRef, string head, params string[] paths)
    {
        _changes[(checkout, baseRef, head)] = [.. paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> Between(string checkout, string baseRef, string head, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add($"{checkout} {baseRef}...{head}");
        if (string.Equals(baseRef, head, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(Results.Success<IReadOnlyList<string>>([]));
        }

        return ValueTask.FromResult(_changes.TryGetValue((checkout, baseRef, head), out var paths)
            ? Results.Success(paths)
            : SindriErrors.InvalidInput.With($"{baseRef}...{head} is not a pair of refs of {checkout}"));
    }
}
