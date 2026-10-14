using Aw.Kernel.Services;
using Aw.Sindri.Features.RegisterRecipe;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflow/recipes (engine spec §6.3; desk): the body is the recipe text and the file name its findings name.</summary>
internal static class RegisterRecipeEndpoint
{
    internal static RouteGroupBuilder MapRegisterRecipe(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/recipes", static async (RegisterRecipeRequest request, IService<RegisterRecipeRequest, RegisterRecipeResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
