using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Features.CloseItem;

/// <summary>The shape of a close (API spec §3.2): a reason that is one of the two defined ones, and an item version of at least 1 (items start at 1, so a lower one can only be a mistake, not a stale read). The JSON binding refuses numbers, so this is for a request built in code.</summary>
internal sealed class CloseItemValidator : IValidator<CloseItemRequest>
{
    public Result<Unit> Validate(CloseItemRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return !CloseReasons.TryTarget(value.Reason, out _)
            ? SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"{(int)value.Reason} isn't a close reason (fix: done or cancelled)"))
            : value.ItemVersion < 1
                ? SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"itemVersion {value.ItemVersion} can't match an item (fix: the version item detail shows, from 1)"))
                : Unit.Value;
    }
}
