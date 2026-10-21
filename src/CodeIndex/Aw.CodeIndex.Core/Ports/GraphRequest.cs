using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>What the graph layer runs on: the filtered inventory of a commit, so the index's deny globs apply and nothing is read from the working tree.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Commit">The commit.</param>
/// <param name="Files">The kept files; graphify's nodes and files are kept only when they name one of these.</param>
/// <param name="Blobs">Where the file contents come from.</param>
/// <param name="Settings">The graph settings.</param>
public sealed record GraphRequest(RepoName Repo, CommitSha Commit, IReadOnlyList<SnapshotFile> Files, IBlobReader Blobs, GraphSettings Settings);
