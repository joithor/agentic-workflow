using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.Team;

/// <summary>Team and lane names become session names ("team-lane"), so they are plain words.</summary>
internal sealed class TeamInitValidator : IValidator<TeamInitRequest>
{
    public Result<Unit> Validate(TeamInitRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Problem(value) is { } problem ? MetaErrors.Invalid.With(problem) : Unit.Value;
    }

    private static string? Problem(TeamInitRequest value)
    {
        if (!IsWord(value.Team) || !IsWord(value.MetaLane))
        {
            return "the team and the meta lane must be letters, digits, '.', '_' or '-'";
        }

        if (string.IsNullOrWhiteSpace(value.Repo))
        {
            return "a repo directory is required";
        }

        if (value.Lanes.Count == 0 || value.Lanes.Any(lane => !IsWord(lane)))
        {
            return "give at least one lane; lane names must be letters, digits, '.', '_' or '-'";
        }

        return new HashSet<string>(value.Lanes, StringComparer.OrdinalIgnoreCase).Count == value.Lanes.Count ? null : "lane names must be unique";
    }

    private static bool IsWord(string text) =>
        text.Length > 0 && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
