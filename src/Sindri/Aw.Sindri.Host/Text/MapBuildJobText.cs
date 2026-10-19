using System.Globalization;
using System.Text;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Text;

/// <summary>The text of a finished map build job: the draft line and one 'degraded:' line for each thing the build did without, reason included ('clusters: timed out after 60 min').</summary>
internal static class MapBuildJobText
{
    internal static string Render(BuildContextMapResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var text = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"draft {result.Draft} for {result.Repo} · {result.Proposed} proposed\n"));
        foreach (var note in result.Degraded)
        {
            _ = text.Append("degraded: ").AppendLine(note);
        }

        return text.ToString();
    }
}
