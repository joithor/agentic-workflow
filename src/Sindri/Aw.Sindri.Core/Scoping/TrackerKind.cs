namespace Aw.Sindri.Core.Scoping;

/// <summary>What a tracker key names (scoping spec §4, C8).</summary>
public enum TrackerKind
{
    /// <summary>One issue.</summary>
    Issue,

    /// <summary>A project, with its issues as children.</summary>
    Project,
}
