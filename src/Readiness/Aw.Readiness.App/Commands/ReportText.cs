using Aw.Readiness.App.Features.CheckRepo;

namespace Aw.Readiness.App.Commands;

/// <summary>The text report: words for every state (colour never carries meaning, so NO_COLOR has nothing to strip).</summary>
internal static class ReportText
{
    internal static string Render(CheckRepoResponse report) => throw new NotSupportedException("red: ReportText for " + report.Scope);
}
