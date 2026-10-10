namespace Aw.Kernel.Testing;

/// <summary>
/// The base of every test-data builder (spec §4b.5): a subclass holds deterministic defaults and fluent
/// 'WithX' methods; 'A.Thing().WithSize(S).Build()'. No shared mutable fixtures: a builder is made per use.
/// </summary>
/// <typeparam name="TSelf">The builder itself.</typeparam>
/// <typeparam name="T">What it builds.</typeparam>
public abstract class Builder<TSelf, T>
    where TSelf : Builder<TSelf, T>
{
    /// <summary>Applies a one-off change to this builder.</summary>
    /// <param name="change">The change.</param>
    /// <returns>This builder.</returns>
    public TSelf With(Action<TSelf> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var self = (TSelf)this;
        change(self);
        return self;
    }

    /// <summary>Builds the value from the current settings.</summary>
    /// <returns>The value.</returns>
    public abstract T Build();

    /// <summary>Builds <paramref name="count"/> values, calling <paramref name="vary"/> with the index (0-based) before each.</summary>
    /// <param name="count">How many.</param>
    /// <param name="vary">Changes the builder per index; the changes accumulate.</param>
    /// <returns>The values, in order.</returns>
    public IReadOnlyList<T> BuildMany(int count, Action<TSelf, int> vary)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(vary);
        var self = (TSelf)this;
        var built = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            vary(self, i);
            built.Add(Build());
        }

        return built;
    }
}
