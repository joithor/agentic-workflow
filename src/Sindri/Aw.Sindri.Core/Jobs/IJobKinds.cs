using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>The kinds of job this build can start: the catalog action ids of the commands registered as jobs. A jobs list filtered by any other kind is a bad filter.</summary>
public interface IJobKinds
{
    /// <summary>Every registered kind, ordered.</summary>
    public IReadOnlyList<JobKind> Known { get; }
}
