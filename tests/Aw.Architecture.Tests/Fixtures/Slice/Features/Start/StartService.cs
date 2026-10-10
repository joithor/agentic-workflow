using Aw.Architecture.Tests.Fixtures.Slice.Features.List;

namespace Aw.Architecture.Tests.Fixtures.Slice.Features.Start;

internal sealed class StartService
{
    public static int Limit => new ListRequest(3).Limit;
}
