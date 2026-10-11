using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListCommands;

[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "--slice-only: unused until this branch registers the slice; remove with that registration")]
internal sealed class ListCommandsValidator : IValidator<ListCommandsRequest>
{
    public Result<Unit> Validate(ListCommandsRequest value) => Unit.Value;
}
