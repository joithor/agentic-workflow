namespace Aw.CodeIndex.Core.Model;

/// <summary>A dependency row read from a manifest (the 'deps' table, spec §4.2).</summary>
/// <param name="Manifest">The manifest's repo-relative path (package.json, *.csproj, Directory.Packages.props).</param>
/// <param name="Name">The package name.</param>
/// <param name="Version">The version text, scrubbed and cut.</param>
/// <param name="Kind">The dependency kind ('runtime', 'dev', ...).</param>
/// <param name="Tags">Scrubbed, cut tags.</param>
public sealed record DepRecord(string Manifest, string Name, string Version, string Kind, string Tags);
