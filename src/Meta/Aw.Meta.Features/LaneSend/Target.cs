using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.LaneSend;

// The lane being sent to: its team, name, record and agent facts.
internal sealed record Target(string Team, string Lane, LaneRecord Record, LaneAgent Agent)
{
    internal string Session => Team + "-" + Lane;
}
