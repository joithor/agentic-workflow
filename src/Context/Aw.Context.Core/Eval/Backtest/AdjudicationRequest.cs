using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>One sample's surfaces to judge against its brief.</summary>
/// <param name="Brief">The brief, cleaned at ingest.</param>
/// <param name="Surfaces">The surfaces no PR touched.</param>
/// <param name="Egress">The scrubber every sent string passes (spec §7 Egress).</param>
/// <param name="TokensLeft">What may still be spent in this run (the 1.5M cap less what was spent).</param>
public sealed record AdjudicationRequest(string Brief, IReadOnlyList<Surface> Surfaces, IEgressScrubber Egress, int TokensLeft);
