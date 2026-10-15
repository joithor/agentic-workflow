using Aw.Context.Core.Engine;

namespace Aw.Context.Infrastructure.Review;

/// <summary>The OS user of this process.</summary>
internal sealed class OsCurrentUser : ICurrentUser
{
    public string Name => string.IsNullOrWhiteSpace(Environment.UserName) ? "unknown" : Environment.UserName;
}
