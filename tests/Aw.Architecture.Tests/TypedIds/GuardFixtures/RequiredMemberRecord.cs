namespace Aw.Kernel.Tests.TypedIds.GuardFixtures;

/// <summary>Guard fixture: a required member and a nullable id both pass.</summary>
internal sealed class RequiredMemberRecord
{
    public required ItemId Item { get; init; }

    public RunId? Run { get; init; }
}
