using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IHeavyReserve over the two files the heavy-job wrapper reads in the state root: heavy-slots-cap (the box-wide slots, default 3)
/// and heavy-meta-reserve (the slots the meta gates hold, 0 to the cap). A write goes to a temporary file that is renamed over the real one.
/// </summary>
/// <param name="stateRoot">$AW_STATE_DIR.</param>
internal sealed class FileHeavyReserve(string stateRoot) : IHeavyReserve
{
    private const string CapFile = "heavy-slots-cap";
    private const string ReserveFile = "heavy-meta-reserve";

    public async ValueTask<Result<HeavySlots>> Read(CancellationToken cancellationToken)
    {
        var cap = await Number(CapFile, cancellationToken).ConfigureAwait(false) is > 0 and var configured ? configured : IHeavyReserve.DefaultCap;
        var reserve = await Number(ReserveFile, cancellationToken).ConfigureAwait(false) ?? 0;
        return new HeavySlots(cap, Math.Min(reserve, cap));
    }

    public async ValueTask<Result<Unit>> Write(int slots, CancellationToken cancellationToken)
    {
        var path = Path.Combine(stateRoot, ReserveFile);
        var temp = path + ".tmp";
        try
        {
            _ = Directory.CreateDirectory(stateRoot);
            await File.WriteAllTextAsync(temp, slots.ToString(CultureInfo.InvariantCulture) + "\n", cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            return Results.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.HostUnreadable.With($"{path}: {exception.Message}");
        }
    }

    // A whole number in the file, or null when the file is missing, unreadable or holds anything else (the wrapper treats those as unset).
    private async ValueTask<int?> Number(string name, CancellationToken cancellationToken)
    {
        try
        {
            var text = (await File.ReadAllTextAsync(Path.Combine(stateRoot, name), cancellationToken).ConfigureAwait(false)).Trim();
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
