using System.Diagnostics.CodeAnalysis;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Microsoft.Extensions.DependencyInjection;

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
        => services.AddValidated<TCommand, TResult, TService>();

    /// <summary>Adds <typeparamref name="TService"/> as the query <typeparamref name="TQuery"/>'s service, behind validation.</summary>
    /// <param name="services">The service collection.</param>
    /// <typeparam name="TQuery">The query record.</typeparam>
    /// <typeparam name="TView">The view it answers.</typeparam>
    /// <typeparam name="TService">The handler.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddQuery<TQuery, TView, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
        where TQuery : IQuery<TView>
        where TService : class, IService<TQuery, TView>
        => services.AddValidated<TQuery, TView, TService>();

    /// <summary>Adds <typeparamref name="TService"/> as the issue <typeparamref name="TIssue"/>'s service, behind the command chain.</summary>
    /// <param name="services">The service collection.</param>
    /// <typeparam name="TIssue">The issue record.</typeparam>
    /// <typeparam name="TResult">What it answers.</typeparam>
    /// <typeparam name="TService">The handler.</typeparam>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddIssue<TIssue, TResult, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
        where TIssue : IIssue<TResult>
        where TService : class, IService<TIssue, TResult>
        => services.AddValidated<TIssue, TResult, TService>();

    private static IServiceCollection AddValidated<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
        where TRequest : notnull
        where TService : class, IService<TRequest, TResponse>
        => services
            .AddScoped<TService>()
            .AddScoped<IService<TRequest, TResponse>>(static sp =>
                new ValidatingService<TRequest, TResponse>(sp.GetRequiredService<TService>(), sp.GetRequiredService<IValidator<TRequest>>()));
}
