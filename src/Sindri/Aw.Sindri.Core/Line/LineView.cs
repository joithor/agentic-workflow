using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>
/// The line board's read model (line spec LN-API1): the packet chain, oldest station first, then one row per phase of the plan in plan order, and the two limits the dispatcher
/// holds the line to. A purged run area shows its frozen packets only (LC-8).
/// </summary>
/// <param name="Stations">Scoping, design and plan, in that order.</param>
/// <param name="Phases">The phase board's rows, in plan order.</param>
/// <param name="Parallel">At most this many phases run at once (<c>workflow.line.parallel</c>).</param>
/// <param name="MaxOpenPrs">At most this many phases are shipped and unmerged (<c>workflow.line.maxOpenPrs</c>).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LineView(IReadOnlyList<LineStation> Stations, IReadOnlyList<LinePhaseView> Phases, int Parallel, int MaxOpenPrs);

/// <summary>One station of the line.</summary>
/// <param name="Station">'scoping', 'design' or 'plan'.</param>
/// <param name="Instance">The station's live child instance address; null once it ended or before it started.</param>
/// <param name="Packet">The packet the station produced; null before it has one.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LineStation(string Station, string? Instance, LinePacket? Packet);

/// <summary>A station's packet and whether a freeze record holds its hash.</summary>
/// <param name="Kind">The packet's kind.</param>
/// <param name="Hash">The canonical hash, 'sha256:…'.</param>
/// <param name="Frozen">Whether aw-packets froze that hash (LN-PKT2's lookup).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinePacket(string Kind, string Hash, bool Frozen);

/// <summary>One phase of the line, as the board and its linked instance show it.</summary>
/// <param name="Id">The phase's id as the plan names it.</param>
/// <param name="Attempt">The attempt that started last; 0 before the first.</param>
/// <param name="Address">The linked instance '{line}/phase.{id}.{attempt}'; null before the phase starts.</param>
/// <param name="State">'waiting', 'running', 'shipped', 'merged', 'failed' or 'skipped'.</param>
/// <param name="ParkReason">Why the phase is parked; null when it is not.</param>
/// <param name="Pr">The phase's pull request link; null before it ships.</param>
/// <param name="Base">What the phase builds on: the head branch of the stack parent, or 'default-branch'; null before the phase starts.</param>
/// <param name="Worktree">The phase's worktree path (LN-WF2), to open in Warp; null before its seed.</param>
/// <param name="Now">What the phase's instance does now (its next sentence); empty before it starts.</param>
/// <param name="Tokens">The tokens the phase's instance has spent.</param>
/// <param name="WallMinutes">Minutes from the phase's start to now, or to its end.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinePhaseView(
    string Id,
    int Attempt,
    string? Address,
    string State,
    string? ParkReason,
    string? Pr,
    string? Base,
    string? Worktree,
    string Now,
    int Tokens,
    int WallMinutes);
