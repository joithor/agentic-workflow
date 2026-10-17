using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Config;

/// <summary>IMetaConfig over the team config file. Fields it doesn't know are ignored; a save replaces the file whole.</summary>
/// <param name="path">The config file.</param>
/// <param name="json">The AwJson options of the host's context.</param>
internal sealed class FileMetaConfig(string path, JsonSerializerOptions json) : IMetaConfig
{
    private static readonly byte[] _utf8Bom = [0xEF, 0xBB, 0xBF];

    public async ValueTask<Result<TeamConfig>> Load(CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return MetaErrors.NoTeamConfig.With($"no team config at {path}; run aw-meta team-init");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.ConfigInvalid.With($"{path}: {exception.Message}");
        }

        var config = AwJson.Deserialize<TeamConfig>(bytes.AsSpan().StartsWith(_utf8Bom) ? bytes.AsSpan(_utf8Bom.Length) : bytes, json);
        if (config.IsError)
        {
            return MetaErrors.ConfigInvalid.With($"{path}: {config.Error.Message}");
        }

        return ThresholdChecks.Problem(config.Value) is { } problem ? MetaErrors.ConfigInvalid.With($"{path}: {problem}") : config;
    }

    public async ValueTask<Result<Unit>> Save(TeamConfig config, CancellationToken cancellationToken)
    {
        var temp = path + ".tmp";
        try
        {
            if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } directory)
            {
                _ = Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(temp, AwJson.Serialize(config, json), cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            return Results.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.ConfigInvalid.With($"{path}: {exception.Message}");
        }
    }
}
