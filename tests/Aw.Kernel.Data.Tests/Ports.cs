using Aw.Kernel.Persistence;

namespace Aw.Kernel.Data.Tests;

/// <summary>Views an adapter through the port a Core project sees, so tests prove the adapter satisfies it.</summary>
public static class Ports
{
    public static IReadRepository<TEntity, TId> Read<TEntity, TId>(IReadRepository<TEntity, TId> repository)
        where TId : struct, IEquatable<TId> => repository;

    public static IUnitOfWorkFactory UnitOfWork(IUnitOfWorkFactory factory) => factory;
}
