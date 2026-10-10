using System.Text.Json;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;

namespace Aw.Judge.Infrastructure.Config;

/// <summary>
/// IJudgeConfigStore over $AW_STATE_DIR/judge/config.json: read leniently (a missing or unreadable file is the default
/// config, as in TS), written as indented Kernel-profile JSON.
/// </summary>
/// <param name="stateDirectory">$AW_STATE_DIR/judge (JudgeSettings.StateDirectory).</param>
/// <param name="json">The host's Kernel JSON profile (its context knows JudgeConfig).</param>
internal sealed class FileJudgeConfigStore(string stateDirectory, JsonSerializerOptions json) : IJudgeConfigStore
{
    internal const string FileName = "config.json";

    private readonly JsonSerializerOptions _indented = AwJson.CreateOptions(
        json.TypeInfoResolver ?? throw new ArgumentException("The options need the host's JSON context.", nameof(json)), indented: true);

    private readonly string _file = Path.Combine(stateDirectory, FileName);

    public async ValueTask<Result<JudgeConfig>> Load(CancellationToken cancellationToken)
    {
        string? text;
        try
        {
            text = await File.ReadAllTextAsync(_file, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            text = null;   // no file, a directory in its place, no permission: the defaults
        }

        return JudgeConfigParser.Parse(text);
    }

    public async ValueTask<Result<Unit>> Save(JudgeConfig config, CancellationToken cancellationToken)
    {
        try
        {
            _ = Directory.CreateDirectory(stateDirectory);
            await File.WriteAllBytesAsync(_file, AwJson.Serialize(config, _indented), cancellationToken).ConfigureAwait(false);
            return Results.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DataErrors.Unavailable.With($"Can't write the judge config '{_file}': {exception.Message}");
        }
    }
}
