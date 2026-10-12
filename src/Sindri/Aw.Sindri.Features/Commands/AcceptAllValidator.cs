using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Commands;

/// <summary>The default validator a command or query may register when it has no input rule (API spec §2.1): accepts everything.</summary>
/// <typeparam name="T">The request record.</typeparam>
internal sealed class AcceptAllValidator<T> : IValidator<T>
{
    public Result<Unit> Validate(T value) => Unit.Value;
}
