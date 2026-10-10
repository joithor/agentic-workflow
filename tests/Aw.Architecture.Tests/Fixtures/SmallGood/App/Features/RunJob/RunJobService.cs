using Aw.Architecture.Tests.Fixtures.SmallGood.Core;

namespace Aw.Architecture.Tests.Fixtures.SmallGood.App.Features.RunJob;

internal sealed class RunJobService
{
    public static Job Run(string name) => new(name);
}
