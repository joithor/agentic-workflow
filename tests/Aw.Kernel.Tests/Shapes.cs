namespace Aw.Kernel.Tests;

/// <summary>Record and class shapes that pin what the AwJson profile does and does not guarantee.</summary>
internal static class Shapes
{
    internal sealed record ItemStarted(string Item);

    internal sealed record Profile(
        string Name,
        int Count,
        double Ratio,
        decimal Money,
        long Big,
        DateTimeOffset At,
        DateTime Day,
        string? Note,
        IReadOnlyList<string> Tags,
        IReadOnlyDictionary<string, int> Counts);

    internal sealed record When(DateTime At);

    internal sealed record Listed(IReadOnlyList<string> Items);

    internal sealed class InitOnly
    {
        // why: shows the limit: a member that is missing from the JSON keeps its initializer, even null!.
        public string Name { get; init; } = null!;
    }

    internal sealed class RequiredInit
    {
        public required string Name { get; init; }
    }
}
