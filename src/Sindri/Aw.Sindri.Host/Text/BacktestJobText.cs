using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Text;

/// <summary>The text of a finished backtest job: the sample count line, one row per arm in report order, the pass line (verdicts are words, never colors).</summary>
internal static class BacktestJobText
{
    internal static string Render(RunContextBacktestResult result) => throw new NotSupportedException();
}
