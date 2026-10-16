namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One arm's numbers in a backtest job's result: the same figures as its row in the report file, without the per-sample detail.</summary>
/// <param name="Samples">The samples it was scored on.</param>
/// <param name="NodeRecall">Mean node recall, or null when not measured.</param>
/// <param name="NodePrecision">Mean node precision, or null when not measured.</param>
/// <param name="FileRecallAt20">Mean file recall@20, or null when not measured.</param>
/// <param name="LeakyShare">The share of samples whose issue changed after its cut.</param>
/// <param name="Tokens">The model tokens the arm spent.</param>
public sealed record BacktestArmScore(int Samples, double? NodeRecall, double? NodePrecision, double? FileRecallAt20, double LeakyShare, int Tokens);
