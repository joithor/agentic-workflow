namespace Aw.Sindri.Core.BugFix;

/// <summary>What a bugfix-state call answered: its value, a refusal of a rule, or an unavailable tool. A refusal is an answer a Step reads, not an error. Made with <see cref="BugFixAnswers"/>.</summary>
/// <typeparam name="T">The value of a call that went through.</typeparam>
public sealed record BugFixAnswer<T>
{
    internal BugFixAnswer()
    {
    }

    /// <summary>How the call ended.</summary>
    public BugFixAnswerStatus Status { get; internal init; }

    /// <summary>The value when <see cref="Status"/> is Ok.</summary>
    public T? Value { get; internal init; }

    /// <summary>The refusal when <see cref="Status"/> is Refused.</summary>
    public BugFixRefusal? Refusal { get; internal init; }

    /// <summary>Why the tool could not run when <see cref="Status"/> is Unavailable.</summary>
    public string? Unavailable { get; internal init; }

    /// <summary>Carries on with the value of a call that went through; a refusal or an unavailable tool stays as it is.</summary>
    /// <typeparam name="TNext">The next call's value.</typeparam>
    /// <param name="next">What to do with the value.</param>
    /// <returns>The next answer, or this one's refusal or failure.</returns>
    public BugFixAnswer<TNext> Bind<TNext>(Func<T, BugFixAnswer<TNext>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (Refusal is { } refusal)
        {
            return BugFixAnswers.Refused<TNext>(refusal);
        }

        if (Unavailable is { } reason)
        {
            return BugFixAnswers.Failed<TNext>(reason);
        }

        return Value is { } value ? next(value) : BugFixAnswers.Failed<TNext>("bugfix-state answered with no value");
    }
}
