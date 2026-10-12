using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Features.RegisterRecipe;

/// <summary>
/// Registers a recipe by hash (engine spec §4.1, §6.3 'recipe register', §8, §9.1; desk). It reads and validates the
/// text against this build's catalog and the registry view (RCP-VERSION-001, -002 and, for a recipe that names a
/// registered base, RCP-CAP-001 to -006 and RCP-CAP-003), with its child recipes resolved from the registered ones. A
/// failing recipe is SND-RECIPE-001 with every rule line, or SND-RECIPE-007 when its id and version are registered
/// with other content, and records nothing. A passing one is stored as its normalized long form, then
/// 'recipe.registered' is appended; registering the same hash again adds nothing and returns the earlier event.
/// </summary>
/// <param name="reader">Reads recipe text.</param>
/// <param name="store">The registered recipes.</param>
/// <param name="registry">The validation context against the registry, and where the stored recipe stands.</param>
/// <param name="events">Appends 'recipe.registered'.</param>
/// <param name="json">The package's AwJson options (they write the normalized form).</param>
/// <param name="clock">When it was registered.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class RegisterRecipeService(
    IRecipeReader reader,
    IRecipeStore store,
    RecipeRegistryReader registry,
    RecipeEvents events,
    JsonSerializerOptions json,
    TimeProvider clock,
    ICallerContextAccessor callers)
    : IService<RegisterRecipeRequest, RegisterRecipeResponse>
{
    public async ValueTask<Result<RegisterRecipeResponse>> Handle(RegisterRecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("RegisterRecipe needs a caller");
        }

        var validated = await Validate(request, cancellationToken).ConfigureAwait(false);
        if (validated.IsError)
        {
            return validated.Error;
        }

        var recipe = validated.Value.Recipe;
        var text = Encoding.UTF8.GetString(AwJson.Serialize(recipe with { Hash = null }, json));
        var stored = await store.Add(new RegisteredRecipe(recipe.Id, recipe.Version, validated.Value.Hash, text, recipe.Base, clock.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
        var appended = stored.IsError ? stored.Error : await events.Registered(stored.Value.Recipe, cancellationToken).ConfigureAwait(false);
        var line = appended.IsError ? appended.Error : await registry.Summary(stored.Value.Recipe, cancellationToken).ConfigureAwait(false);
        return line.IsError
            ? line.Error
            : new RegisterRecipeResponse(new CommandAccepted(caller.CommandId, appended.Value.Sequence), recipe.Id, recipe.Version, validated.Value.Hash,
                stored.Value.Added, line.Value.Standing);
    }

    /// <summary>The bound document when it passes against the registry; else SND-RECIPE-007 for RCP-VERSION-001, or SND-RECIPE-001.</summary>
    private async ValueTask<Result<RecipeDocument>> Validate(RegisterRecipeRequest request, CancellationToken cancellationToken)
    {
        var reading = reader.Read(request.Text, request.File);
        if (reading.Document is not { } document)
        {
            return Refused(request.File, reading.Findings);
        }

        var context = await registry.Validation(document.Recipe, cancellationToken).ConfigureAwait(false);
        if (context.IsError)
        {
            return context.Error;
        }

        List<RecipeFinding> findings = [.. reading.Findings, .. RecipeValidator.Validate(document, context.Value)];
        return RecipeValidator.Passes(findings) ? document : Refused(request.File, findings);
    }

    private static Error Refused(string file, IEnumerable<RecipeFinding> findings)
    {
        var errors = findings.Where(static f => f.Severity == FindingSeverity.Error).ToList();
        var lines = string.Concat(errors.Select(static f => "\n  " + f.ToLine()));
        return errors.Exists(static f => f.Rule == "RCP-VERSION-001")
            ? WorkflowErrors.RecipeVersionTaken.With($"{file}'s id and version are registered with other content; nothing was registered:{lines}")
            : WorkflowErrors.RecipeInvalid.With($"{file} doesn't validate; nothing was registered. Fix each line, then register again:{lines}");
    }
}
