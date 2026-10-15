using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>The shape of an item to create (API spec §3.2): a title of 1–200 characters after trimming with no control character, a body of at most 4,000.</summary>
internal sealed class CreateItemValidator : IValidator<CreateItemRequest>
{
    private const int MaxTitle = 200;
    private const int MaxBody = 4000;

    public Result<Unit> Validate(CreateItemRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var title = value.Title?.Trim() ?? string.Empty;
        return title.Length is 0 or > MaxTitle
            ? SindriErrors.InvalidInput.With($"the title is {title.Length} characters (fix: 1 to {MaxTitle})")
            : title.Any(char.IsControl)
                ? SindriErrors.InvalidInput.With("the title has a control character (fix: plain text on one line)")
                : value.Body is { Length: > MaxBody }
                    ? SindriErrors.InvalidInput.With($"the body is {value.Body.Length} characters (fix: at most {MaxBody})")
                    : Unit.Value;
    }
}
