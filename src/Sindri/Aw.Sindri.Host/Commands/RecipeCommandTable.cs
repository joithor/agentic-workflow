using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'recipe' verb group's table (spec §6.3; its text is Recipe/RecipeText). 'validate' and 'eval' are local: they
/// run in this process against the build's catalog and never start or contact serve; 'eval --record' is a query
/// against serve and refuses until P4. Checker exit codes (W27): 0 pass, 1 fail, 2 usage or tool error.
/// </summary>
internal static partial class RecipeCommandTable
{
    private const string ValidateUsage = "usage: sindri recipe validate <file> [--json]";
    private const string EvalUsage = "usage: sindri recipe eval <file> [--json] | --scaffold <file> | --record <address> --out <file>";
    private const string ApproveUsage = "usage: sindri recipe approve <file|id@hash>";
    private const string RevokeUsage = "usage: sindri recipe revoke <id@hash>";

    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => Run(args, services, output, desk: null, ct);

    /// <summary>Runs a 'recipe' verb; the registry's server verbs (approve, revoke) need <paramref name="desk"/>.</summary>
    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, RecipeDesk? desk, CancellationToken ct) => args switch
    {
        ["validate", "--json"] or ["validate"] => output.Error(SindriErrors.InvalidInput.With($"recipe validate needs a recipe file ({ValidateUsage})")),
        ["validate", var file] => Validate(file, json: false, services, output, ct),
        ["validate", var file, "--json"] => Validate(file, json: true, services, output, ct),
        ["validate", "--json", var file] => Validate(file, json: true, services, output, ct),
        ["eval", .. var rest] => RunEval(rest, services, output, ct),
        ["register", ..] => Pending("register", "(b)", output),
        ["show", ..] => Pending("show", "(b)", output),
        ["list", ..] => Pending("list", "(b)", output),
        ["approve", var target] when !IsFlag(target) && desk is not null => new RecipeDeskCommands(desk, services, output).Approve(target, ct),
        ["approve", ..] => output.Error(SindriErrors.InvalidInput.With($"recipe approve needs one recipe file or id@hash ({ApproveUsage})")),
        ["revoke", var target] when target.Contains('@', StringComparison.Ordinal) && desk is not null => new RecipeDeskCommands(desk, services, output).Revoke(target, ct),
        ["revoke", ..] => output.Error(SindriErrors.InvalidInput.With($"recipe revoke needs one id@hash ({RevokeUsage})")),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["recipe", .. args])}' is not a sindri command ({ValidateUsage}; {EvalUsage})")),
    };

    private static Task<int> RunEval(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        ["--record", _, "--out", _] => output.Error(SindriErrors.UnknownVerb.With(
            "recipe eval --record needs the server (P4): it reads a live instance's history from serve, which arrives with P4 (fix: write the case by hand, or start from sindri recipe eval --scaffold <file>)")),
        ["--record", ..] => output.Error(SindriErrors.InvalidInput.With($"recipe eval --record needs an address and --out <file> ({EvalUsage})")),
        [] or ["--json"] or ["--scaffold"] => output.Error(SindriErrors.InvalidInput.With($"recipe eval needs a recipe file ({EvalUsage})")),
        ["--scaffold", var file] when !IsFlag(file) => Eval(new EvalRecipeRequest(file, EvalMode.Scaffold), json: false, services, output, ct),
        ["--json", var file] when !IsFlag(file) => Eval(new EvalRecipeRequest(file, EvalMode.Run), json: true, services, output, ct),
        [var file, "--json"] when !IsFlag(file) => Eval(new EvalRecipeRequest(file, EvalMode.Run), json: true, services, output, ct),
        [var file] when !IsFlag(file) => Eval(new EvalRecipeRequest(file, EvalMode.Run), json: false, services, output, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["recipe", "eval", .. args])}' is not a sindri command ({EvalUsage})")),
    };

    /// <summary>P2 contract: the registry verbs are listed, and each says which unit ships it.</summary>
    private static Task<int> Pending(string verb, string unit, Output output) =>
        output.Error(SindriErrors.UnknownVerb.With($"recipe {verb} arrives with engine P2 unit {unit} (fix: sindri recipe validate <file> checks a recipe now)"));

    private static bool IsFlag(string arg) => arg.StartsWith("--", StringComparison.Ordinal);

    private static async Task<int> Validate(string file, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        Result<ValidateRecipeResponse> result;
        try
        {
            result = await services.GetRequiredService<IService<ValidateRecipeRequest, ValidateRecipeResponse>>()
                .Handle(ValidateRecipeRequest.FromPath(file), ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: a bug in reading or validation is a tool error (W27 exit 2), not a crash
        {
            return await ToolError(services, output, "recipe validate", e, file).ConfigureAwait(false);
        }

        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var response = result.Value;
        var code = response.Passed ? 0 : 1;
        return json
            ? await output.Json(RecipeValidationView.From(response, services.GetRequiredService<JsonSerializerOptions>()), code).ConfigureAwait(false)
            : await output.Text(RecipeText.Validation(response), code).ConfigureAwait(false);
    }

    private static async Task<int> Eval(EvalRecipeRequest request, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        Result<EvalRecipeResponse> result;
        try
        {
            result = await services.GetRequiredService<IService<EvalRecipeRequest, EvalRecipeResponse>>()
                .Handle(request, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: a bug in reading or folding is a tool error (W27 exit 2), not a crash
        {
            return await ToolError(services, output, "recipe eval", e, request.Path).ConfigureAwait(false);
        }

        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var response = result.Value;
        if (request.Mode == EvalMode.Scaffold)
        {
            return await output.Text(response.Scaffold ?? string.Empty, 0).ConfigureAwait(false);
        }

        var code = response.Passed ? 0 : 1;
        return json
            ? await output.Json(response, code).ConfigureAwait(false)
            : await output.Text(RecipeText.Eval(response), code).ConfigureAwait(false);
    }

    private static Task<int> ToolError(IServiceProvider services, Output output, string verb, Exception e, string file)
    {
        LogUnhandled(services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RecipeCommandTable)), e, verb, file);
        return output.Error(SindriErrors.ApiInternal.With(
            $"{verb} failed inside sindri: {e.GetType().Name}: {e.Message} (fix: this is a sindri bug; report it with the recipe file)"));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "SND-API-005 {Verb} {File} failed with an unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string verb, string file);
}
