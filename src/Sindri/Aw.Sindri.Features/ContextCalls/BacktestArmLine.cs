namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One arm in a backtest job's result.</summary>
/// <param name="Arm">The arm's name.</param>
/// <param name="NotAvailable">The stage that brings an arm that doesn't run yet, else null.</param>
/// <param name="Overall">The arm's overall line ('PASS', 'NOT PASSED', 'not measured', the owner-decision line), or null when it did not run.</param>
public sealed record BacktestArmLine(string Arm, string? NotAvailable, string? Overall);
