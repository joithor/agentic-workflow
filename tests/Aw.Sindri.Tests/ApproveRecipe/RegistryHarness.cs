using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Challenges;
using Aw.Sindri.Testing.Workflow;
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
            .AddSingleton(SliceHarness.Json)
            .AddSingleton<IProfileSource>(H.Profiles)
            .AddRecipeBlocks()
            .AddSingleton<BlockCatalog>()
            .AddScoped<RecipeApprovalPlanner>()
            .AddScoped<RecipeRevokePlanner>()
            .AddSingleton<Core.Events.RecipeEvents>()
            .AddScoped<IValidator<ApproveRecipeRequest>, AcceptAllValidator<ApproveRecipeRequest>>().AddCommand<ApproveRecipeRequest, ApproveRecipeResponse, ApproveRecipeService>()
            .AddScoped<IValidator<RevokeRecipeRequest>, AcceptAllValidator<RevokeRecipeRequest>>().AddCommand<RevokeRecipeRequest, RevokeRecipeResponse, RevokeRecipeService>()
            .AddScoped<IValidator<IssueChallengeRequest>, AcceptAllValidator<IssueChallengeRequest>>().AddIssue<IssueChallengeRequest, IssueChallengeResponse, IssueChallengeService>()
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

    /// <summary>Registers <paramref name="recipe"/> under the hash of its normalized long form.</summary>
    public async Task<RegisteredRecipe> Register(Recipe recipe)
    {
        var hash = RecipeHashing.Of(recipe, SliceHarness.Json);
        var text = Encoding.UTF8.GetString(AwJson.Serialize(recipe with { Hash = null }, SliceHarness.Json));
        return (await Recipes.Add(new RegisteredRecipe(recipe.Id, recipe.Version, hash, text, recipe.Base, H.Clock.GetUtcNow()),
            TestContext.Current.CancellationToken)).Value.Recipe;
    }

    /// <summary>The recipe a registered row's normalized text reads back as.</summary>
    public static Recipe Parse(RegisteredRecipe registered) => AwJson.Deserialize<Recipe>(Encoding.UTF8.GetBytes(registered.Text), SliceHarness.Json).Value;

    /// <summary>Runs <paramref name="request"/> through its registered chain as <paramref name="caller"/>.</summary>
    public async Task<Result<TResponse>> Run<TRequest, TResponse>(TRequest request, CallerContext caller)
        where TRequest : notnull
    {
        using var entered = H.Callers.Enter(caller);
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IService<TRequest, TResponse>>().Handle(request, TestContext.Current.CancellationToken);
    }

    /// <summary>A human at a terminal, as the CLI calls.</summary>
    public static CallerContext Human { get; } = A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

    /// <summary>Registers the shipped recipe at <paramref name="path"/> (relative to recipes/, e.g. 'sindri/workflow.step.yaml').</summary>
    public Task<RegisteredRecipe> Shipped(string path) =>
        Register(Workflow.RecipeReaderTests.Reader.Read(ShippedRecipes.Read(path), Path.GetFileName(path)).Document!);   // why: the shipped recipes bind (ShippedRecipesTests)

    /// <summary>Asks for a challenge for <paramref name="action"/> as <paramref name="caller"/> (a human at a terminal by default).</summary>
    public Task<Result<IssueChallengeResponse>> Issue(string action, CallerContext? caller = null) =>
        Run<IssueChallengeRequest, IssueChallengeResponse>(new IssueChallengeRequest(action), caller ?? Human);

    /// <summary>Approves <paramref name="recipe"/> as a human would: a challenge, its action typed back.</summary>
    public async Task<Result<ApproveRecipeResponse>> Approve(RegisteredRecipe recipe)
    {
        var issued = (await Issue(string.Create(CultureInfo.InvariantCulture, $"approve {recipe.Id.Value} {recipe.Version}"))).Value;
        return await Run<ApproveRecipeRequest, ApproveRecipeResponse>(
            new ApproveRecipeRequest(recipe.Id, recipe.Hash, new ChallengeAnswer(issued.Challenge, issued.Action)), Human);
    }

    /// <summary>The ledger's events of <paramref name="type"/>.</summary>
    public IReadOnlyList<LedgerEvent> Events(string type) => [.. H.Store.Events.Where(e => e.Envelope.Type == type)];

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
