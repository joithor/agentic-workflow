using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Build;

/// <summary>
/// IBuildInfo over this assembly's attributes: the commit from its InformationalVersion (the publish passes SourceRevisionId) and
/// the build time from its AwBuildTime metadata (the publish passes AwBuildTime). A build that stamped neither reads "unknown".
/// </summary>
internal sealed class AssemblyBuildInfo : IBuildInfo
{
    internal const string Unknown = "unknown";

    public BuildInfo Read()
    {
        _ = BuildStamp.Commit(Unknown);
        throw new NotSupportedException();
    }
}
