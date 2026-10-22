namespace Aw.Sindri.Core.BugFix;

/// <summary>Makes <see cref="BugFixAnswer{T}"/>.</summary>
public static class BugFixAnswers
{
    /// <summary>A call that went through.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">What it returned.</param>
    /// <returns>The answer.</returns>
    public static BugFixAnswer<T> Ok<T>(T value) => new() { Status = BugFixAnswerStatus.Ok, Value = value };

    /// <summary>A call a rule declined.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="refusal">The refusal.</param>
    /// <returns>The answer.</returns>
    public static BugFixAnswer<T> Refused<T>(BugFixRefusal refusal) => new() { Status = BugFixAnswerStatus.Refused, Refusal = refusal };

    /// <summary>A call the tool could not make.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="reason">Why, in words the human reads.</param>
    /// <returns>The answer.</returns>
    public static BugFixAnswer<T> Failed<T>(string reason) => new() { Status = BugFixAnswerStatus.Unavailable, Unavailable = reason };
}
