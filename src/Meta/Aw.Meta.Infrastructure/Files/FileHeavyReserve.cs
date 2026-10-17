using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IHeavyReserve over heavy-reserve in the team's state directory: one line, "reserve depth time" (the time ISO 8601, round-trip).
/// A write goes to a temporary file in the same directory that is renamed over the real one.
/// </summary>
/// <param name="directory">The team's state directory.</param>
internal sealed class FileHeavyReserve(string directory) : IHeavyReserve
{
    private const string ReserveFile = "heavy-reserve";

    public async ValueTask<Result<HeavyReserveRecord?>> Read(CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(Path.Combine(directory, ReserveFile), cancellationToken).ConfigureAwait(false);
            return Results.Success(Parse(text));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Results.Success<HeavyReserveRecord?>(value: null);
        }
    }

    public async ValueTask<Result<Unit>> Write(HeavyReserveRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var path = Path.Combine(directory, ReserveFile);
        var temp = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $".{ReserveFile}.{Environment.ProcessId}.{Path.GetRandomFileName()}.tmp"));
        try
        {
            _ = Directory.CreateDirectory(directory);
            var line = string.Create(CultureInfo.InvariantCulture, $"{record.Reserve} {record.Depth} {record.At:O}\n");
            await File.WriteAllTextAsync(temp, line, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            return Results.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            return MetaErrors.HostUnreadable.With($"{path}: {exception.Message}");
        }
    }

    // Anything but three well-formed fields reads as "not computed yet".
    private static HeavyReserveRecord? Parse(string text)
    {
        var fields = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return fields.Length == 3
            && int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var reserve)
            && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var depth)
            && DateTimeOffset.TryParse(fields[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                ? new HeavyReserveRecord(reserve, depth, at)
                : null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temporary file is harmless.
        }
    }
}
