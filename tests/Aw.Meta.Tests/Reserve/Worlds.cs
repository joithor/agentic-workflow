using Aw.Meta.Core.Coordination;
using Aw.Meta.Features.Status;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Reserve;

/// <summary>The queue, reservation and advice services over a world's fakes.</summary>
internal static class Worlds
{
    internal static MergeQueueReader Queue(MetaWorld world) => new(world.Messages, world.Gates, world.Values);

    internal static StatusReader StatusReader(MetaWorld world) => new(world.Reader, Queue(world), world.Owner, world.Lanes);

    internal static HeavyReservation Reservation(MetaWorld world) => new(world.Heavy, Queue(world), world.Log, world.Clock);

    internal static ResourceAdvisor Advisor(MetaWorld world) => new(world.Heavy);
}
