using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>Refuses 'invalid', as a slice's validator would.</summary>
internal sealed class FixtureValidator : IValidator<FixtureCommand>
{
    public Result<Unit> Validate(FixtureCommand value) =>
        value.Outcome == "invalid" ? SindriErrors.InvalidInput.With("outcome 'invalid' is not allowed") : Unit.Value;
}
