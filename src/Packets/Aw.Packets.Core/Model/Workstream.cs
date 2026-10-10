using Aw.Packets.Core.Ids;

namespace Aw.Packets.Core.Model;

/// <summary>A workstream: a group of tasks.</summary>
/// <param name="Id">The workstream id.</param>
/// <param name="Title">What it delivers.</param>
public sealed record Workstream(WorkstreamId Id, string Title);
