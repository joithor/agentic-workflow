using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One input the open run filled: its metadata and the request it was filled from (history already holds both; no text).</summary>
/// <param name="Recorded">The metadata the 'FillInput' activity returned.</param>
/// <param name="Request">What the input was filled from.</param>
internal sealed record InjectionFill(FilledInput Recorded, FillInputRequest Request);

/// <summary>What the 'GetInjection' Query answers: the open run's fills, in declaration order.</summary>
/// <param name="State">The state whose Step runs.</param>
/// <param name="Run">The run token; empty when no run filled inputs.</param>
/// <param name="Fills">The fills.</param>
internal sealed record InjectionSnapshot(string State, string Run, IReadOnlyList<InjectionFill> Fills);
