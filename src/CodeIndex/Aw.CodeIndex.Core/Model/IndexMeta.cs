using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Model;

/// <summary>The index's header (the 'meta' table, spec §4.2): what it describes and what built it.</summary>
/// <param name="Commit">The commit it describes; the only place a recorded head is read from.</param>
/// <param name="DefaultBranch">The default branch the commit was on.</param>
/// <param name="BuiltAt">When the build published.</param>
/// <param name="Schema">The schema version (1); a different one is rebuilt, never migrated.</param>
/// <param name="ParserVersion">The parser version; part of the structure stamp.</param>
/// <param name="LockSha">The sha of the pins file the build used, or null when none.</param>
/// <param name="Counts">Files, symbols and edges, stored so a read never counts.</param>
public sealed record IndexMeta(CommitSha Commit, string DefaultBranch, DateTimeOffset BuiltAt, int Schema, string ParserVersion, string? LockSha, IndexCounts Counts);
