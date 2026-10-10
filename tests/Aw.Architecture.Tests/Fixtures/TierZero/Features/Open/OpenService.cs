using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Architecture.Tests.Fixtures.TierZero.Features.Open;

internal static class OpenService
{
    public static Result<SqliteOptions> Open(string path) => new SqliteOptions(path);
}
