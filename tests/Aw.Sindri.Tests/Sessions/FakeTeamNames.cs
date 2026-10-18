using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Teams as a list.</summary>
internal sealed class FakeTeamNames(params TeamNames[] teams) : ITeamNames
{
    public IReadOnlyList<TeamNames> Read() => teams;
}
