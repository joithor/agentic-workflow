
namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'GetInjection' Query answers: the open run's fills, in declaration order.</summary>
/// <param name="State">The state whose Step runs.</param>
/// <param name="Run">The run token; empty when no run filled inputs.</param>
/// <param name="Fills">The fills.</param>
internal sealed record InjectionSnapshot(string State, string Run, IReadOnlyList<InjectionFill> Fills);
