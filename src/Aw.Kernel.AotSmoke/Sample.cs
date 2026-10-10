namespace Aw.Kernel.AotSmoke;

internal sealed record Sample(
    string Id,
    DateTimeOffset At,
    IReadOnlyList<string> Texts,
    IReadOnlyList<double> Numbers,
    Error Error,
    IReadOnlyDictionary<string, int> Keys);
