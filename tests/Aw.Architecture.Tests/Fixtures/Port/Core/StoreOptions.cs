using Aw.Kernel.Data;

namespace Aw.Architecture.Tests.Fixtures.Port.Core;

internal sealed class StoreOptions
{
    public static SqliteOptions Defaults(string path) => new(path);
}
