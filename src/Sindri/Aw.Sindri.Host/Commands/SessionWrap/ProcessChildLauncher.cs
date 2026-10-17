using Aw.Kernel;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>Starts the agent as a child process on wrap's own terminal.</summary>
internal sealed class ProcessChildLauncher : IChildLauncher
{
    public Result<IWrapChild> Start(IReadOnlyList<string> argv, IReadOnlyDictionary<string, string> environment)
        => throw new NotSupportedException("ProcessChildLauncher: make ProcessChildLauncherTests pass");
}
