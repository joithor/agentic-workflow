namespace Aw.Kernel.AotSmoke;

internal sealed record Sample(
    string Name,
    DateTimeOffset At,
    decimal Money,
    string? Note,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, int> Counts,
    Error Error);
