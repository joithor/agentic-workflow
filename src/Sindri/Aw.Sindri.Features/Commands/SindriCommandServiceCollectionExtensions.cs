using System.Diagnostics.CodeAnalysis;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// Registers commands, queries and issues behind their chains (API spec §2.1), so no slice can skip one. Each slice
/// registers its own IValidator for its request (its validator, or AcceptAllValidator) next to its line.
/// </summary>
public static class SindriCommandServiceCollectionExtensions
{
    /// <summary>Adds <typeparamref name="TService"/> as the command <typeparamref name="TCommand"/>'s service, behind the command chain.</summary>
    /// <param name="services">The service collection.</param>
    /// <typeparam name="TCommand">The command record.</typeparam>
    /// <typeparam name="TResult">What it answers.</typeparam>
    /// <typeparam name="TService">The handler.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddCommand<TCommand, TResult, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
        where TCommand : ICommand<TResult>
        where TService : class, IService<TCommand, TResult>
        => services.AddChained<TCommand, TResult, TService>();

    /// <summary>Adds <typeparamref name="TService"/> as the query <typeparamref name="TQuery"/>'s service, behind validation.</summary>
    /// <param name="services">The service collection.</param>
    /// <typeparam name="TQuery">The query record.</typeparam>
    /// <typeparam name="TView">The view it answers.</typeparam>
    /// <typeparam name="TService">The handler.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddQuery<TQuery, TView, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
        where TQuery : IQuery<TView>
        where TService : class, IService<TQuery, TView>
        => services
            .AddScoped<TService>()
            .AddScoped<IService<TQuery, TView>>(static sp =>
                new QuerySpan<TQuery, TView>(new ValidatingService<TQuery, TView>(sp.GetRequiredService<TService>(), sp.GetRequiredService<IValidator<TQuery>>())));

    /// <summary>Adds <typeparamref name="TService"/> as the issue <typeparamref name="TIssue"/>'s service, behind the command chain.</summary>
    /// <param name="services">The service collection.</param>
    /// <typeparam name="TIssue">The issue record.</typeparam>
    /// <typeparam name="TResult">What it answers.</typeparam>
    /// <typeparam name="TService">The handler.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddIssue<TIssue, TResult, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
        where TIssue : IIssue<TResult>
        where TService : class, IService<TIssue, TResult>
        => services.AddChained<TIssue, TResult, TService>();

    /// <summary>The command chain, outermost first: gate, audit, authorization, validation, waiting for the views, then the handler.</summary>
    private static IServiceCollection AddChained<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
        where TRequest : notnull
        where TService : class, IService<TRequest, TResponse>
    {
        services.TryAddSingleton<CommandGateLock>();
        services.TryAddSingleton<AuditTrail>();
        return services
            .AddScoped<TService>()
            .AddScoped<IService<TRequest, TResponse>>(static sp => Chain<TRequest, TResponse, TService>(sp));
    }

    /// <summary>Resolves and constructs one command's chain (no logic: the order is the point).</summary>
    private static CommandGate<TRequest, TResponse> Chain<TRequest, TResponse, TService>(IServiceProvider sp)
        where TRequest : notnull
        where TService : class, IService<TRequest, TResponse>
    {
        var callers = sp.GetRequiredService<ICallerContextAccessor>();
        var catalog = sp.GetRequiredService<ICommandCatalog>();
        return new CommandGate<TRequest, TResponse>(
            new AuditingService<TRequest, TResponse>(
                new AuthorizingService<TRequest, TResponse>(
                    new ValidatingService<TRequest, TResponse>(
                        new AwaitingViews<TRequest, TResponse>(sp.GetRequiredService<TService>(), catalog,
                            sp.GetRequiredService<IProjectorStatus>(), sp.GetRequiredService<ILedgerStore>()),
                        sp.GetRequiredService<IValidator<TRequest>>()),
                    callers, catalog),
                sp.GetRequiredService<AuditTrail>(), callers, catalog, sp.GetRequiredService<IIdGenerator>()),
            sp.GetRequiredService<CommandGateLock>(), sp.GetRequiredService<TimeProvider>());
    }
}
