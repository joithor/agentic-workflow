using System.Text;
using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>
/// An IEvidenceTargets. Without JSON options it answers with <see cref="Targets"/> (no config by default); with options it holds
/// config files by path and reads them the way the real adapter does (the repo's file, or the --config file).
/// </summary>
public sealed class FakeEvidenceTargets(JsonSerializerOptions? json = null) : IEvidenceTargets
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    /// <summary>The targets every load answers with when the fake was made without JSON options.</summary>
    public ResolvedTargets Targets { get; set; } = ResolvedTargets.Empty;

    /// <summary>The (repo, configPath) of every load.</summary>
    public IList<(string Repo, string? ConfigPath)> Loads { get; } = [];

    /// <summary>Puts a config file in place.</summary>
    /// <param name="path">The file's absolute path.</param>
    /// <param name="content">Its text.</param>
    /// <returns>This fake.</returns>
    public FakeEvidenceTargets File(string path, string content)
    {
        _files[path] = Encoding.UTF8.GetBytes(content);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<ResolvedTargets>> LoadAsync(string repo, string? configPath, CancellationToken cancellationToken)
    {
        Loads.Add((repo, configPath));
        if (json is null)
        {
            return ValueTask.FromResult<Result<ResolvedTargets>>(Targets);
        }

        var path = configPath ?? (repo.TrimEnd('/') + "/" + TargetsFile.RepoRelativePath);
        return ValueTask.FromResult(Read(path, configPath is not null, json));
    }

    private Result<ResolvedTargets> Read(string path, bool isConfig, JsonSerializerOptions options)
    {
        if (!_files.TryGetValue(path, out var bytes))
        {
            return isConfig ? EvidenceErrors.FileNotFound.With($"--config {path} does not exist") : ResolvedTargets.Empty;
        }

        return TargetsReader.Read(bytes, options).Match<Result<ResolvedTargets>>(targets => targets, error => error with { Message = $"targets {path}: {error.Message}" });
    }
}
