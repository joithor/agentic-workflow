using System.Text;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing.Challenges;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>
/// The registry slices behind the real command chain (AddCommand) and the Host's catalog, over in-memory ports: the
/// recipe store, the approvals, the challenges and a recording wake (engine spec Phase 2). P2's units register their
/// slices here as they ship them.
/// </summary>
internal sealed class RegistryHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public RegistryHarness()
    {
        Challenges = new InMemoryChallenges(H.Clock);
        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(H.Clock)
            .AddSingleton<IIdGenerator>(H.Ids)
            .AddSingleton<ILedgerStore>(H.Store)
            .AddSingleton<ILease>(H.Store)
            .AddSingleton(H.Held)
            .AddSingleton(H.Events)
            .AddSingleton(H.Callers)
            .AddSingleton<ICommandCatalog>(new CommandCatalog())
            .AddSingleton<ILogger<AuditTrail>>(new ListLogger<AuditTrail>())
            .AddSingleton<IRecipeStore>(Recipes)
            .AddSingleton<IRecipeApprovals>(Approvals)
            .AddSingleton<IChallenges>(Challenges)
            .AddSingleton<IRecipeWake>(Wake)
            .AddScoped<IValidator<ApproveRecipeRequest>, AcceptAllValidator<ApproveRecipeRequest>>().AddCommand<ApproveRecipeRequest, ApproveRecipeResponse, ApproveRecipeService>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public SliceHarness H { get; } = new();

    public InMemoryRecipeStore Recipes { get; } = new();

    public InMemoryRecipeApprovals Approvals { get; } = new();

    public InMemoryChallenges Challenges { get; }

    public RecordingRecipeWake Wake { get; } = new();

    /// <summary>Registers <paramref name="document"/> in the store as RegisterRecipe would keep it: its normalized long form by hash.</summary>
    public async Task<RegisteredRecipe> Register(RecipeDocument document)
    {
        var recipe = document.Recipe;
        var text = Encoding.UTF8.GetString(AwJson.Serialize(recipe with { Hash = null }, SliceHarness.Json));
        return (await Recipes.Add(new RegisteredRecipe(recipe.Id, recipe.Version, document.Hash, text, recipe.Base, H.Clock.GetUtcNow()),
            TestContext.Current.CancellationToken)).Value.Recipe;
    }

    /// <summary>Runs <paramref name="request"/> through its registered chain as <paramref name="caller"/>.</summary>
    public async Task<Result<TResponse>> Run<TRequest, TResponse>(TRequest request, CallerContext caller)
        where TRequest : notnull
    {
        using var entered = H.Callers.Enter(caller);
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IService<TRequest, TResponse>>().Handle(request, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
