using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.LaneRecycle;

// What a recycle will act on, once every refusal has been checked.
internal sealed record RecyclePlan(string Team, string Lane, SessionId OldSession, long? OldTokens, string Digest)
{
    internal string Session => Team + "-" + Lane;

    internal string ContinueLine => "Read and continue from " + Digest;
}
