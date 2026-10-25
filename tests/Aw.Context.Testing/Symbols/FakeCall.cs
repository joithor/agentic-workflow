namespace Aw.Context.Testing.Symbols;

/// <summary>One call edge the fake index holds.</summary>
/// <param name="From">The calling symbol's ref.</param>
/// <param name="To">The called symbol's ref.</param>
/// <param name="Basis">'semantic' or 'syntax'.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Coverage">'full' or 'partial'.</param>
public sealed record FakeCall(string From, string To, string Basis, double Confidence, string Coverage);
