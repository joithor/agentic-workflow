using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Logs;

namespace Aw.Meta.Features.LogAppend;

internal sealed class AppendLogValidator : IValidator<AppendLogRequest>
{
    public Result<Unit> Validate(AppendLogRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!LogLine.TryParseKind(value.Kind, out _))
        {
            return MetaErrors.Invalid.With("the kind must be merge, decision, lanes, incident or note");
        }

        return string.IsNullOrWhiteSpace(value.Text) ? MetaErrors.Invalid.With("some text is required") : Unit.Value;
    }
}
