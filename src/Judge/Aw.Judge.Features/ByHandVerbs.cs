using Aw.Judge.Features.Adjudicate;
using Aw.Judge.Features.EvaluateQuestion;
using Aw.Judge.Features.GetLabelStatus;
using Aw.Judge.Features.ImportLabels;
using Aw.Judge.Features.ImportTurns;
using Aw.Judge.Features.LabelOutcomes;
using Aw.Judge.Features.SetLabel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Features;

/// <summary>
/// The by-hand verbs of the cutover's unit 6.3: label, adjudicate and eval. Each is a service behind its validator, registered like every other slice.
/// </summary>
internal static class ByHandVerbs
{
    internal static void Add(IServiceCollection services)
    {
        AddLabelling(services);
        AddScoring(services);
    }

    // The label verbs: import, outcomes, set and status (unit 6.3).
    private static void AddLabelling(IServiceCollection services)
    {
        _ = services
            .AddScoped<ImportLabelsService>()
            .AddScoped<IValidator<ImportLabelsRequest>, ImportLabelsValidator>()
            .AddScoped<IService<ImportLabelsRequest, ImportLabelsResponse>>(static sp =>
                new ValidatingService<ImportLabelsRequest, ImportLabelsResponse>(
                    sp.GetRequiredService<ImportLabelsService>(),
                    sp.GetRequiredService<IValidator<ImportLabelsRequest>>()));
        _ = services
            .AddScoped<LabelOutcomesService>()
            .AddScoped<IValidator<LabelOutcomesRequest>, LabelOutcomesValidator>()
            .AddScoped<IService<LabelOutcomesRequest, LabelOutcomesResponse>>(static sp =>
                new ValidatingService<LabelOutcomesRequest, LabelOutcomesResponse>(
                    sp.GetRequiredService<LabelOutcomesService>(),
                    sp.GetRequiredService<IValidator<LabelOutcomesRequest>>()));
        _ = services
            .AddScoped<SetLabelService>()
            .AddScoped<IValidator<SetLabelRequest>, SetLabelValidator>()
            .AddScoped<IService<SetLabelRequest, SetLabelResponse>>(static sp =>
                new ValidatingService<SetLabelRequest, SetLabelResponse>(
                    sp.GetRequiredService<SetLabelService>(),
                    sp.GetRequiredService<IValidator<SetLabelRequest>>()));
        _ = services
            .AddScoped<GetLabelStatusService>()
            .AddScoped<IValidator<GetLabelStatusRequest>, GetLabelStatusValidator>()
            .AddScoped<IService<GetLabelStatusRequest, GetLabelStatusResponse>>(static sp =>
                new ValidatingService<GetLabelStatusRequest, GetLabelStatusResponse>(
                    sp.GetRequiredService<GetLabelStatusService>(),
                    sp.GetRequiredService<IValidator<GetLabelStatusRequest>>()));
    }

    // adjudicate, eval and eval import-turns (unit 6.3).
    private static void AddScoring(IServiceCollection services)
    {
        _ = services
            .AddScoped<AdjudicateService>()
            .AddScoped<IValidator<AdjudicateRequest>, AdjudicateValidator>()
            .AddScoped<IService<AdjudicateRequest, AdjudicateResponse>>(static sp =>
                new ValidatingService<AdjudicateRequest, AdjudicateResponse>(
                    sp.GetRequiredService<AdjudicateService>(),
                    sp.GetRequiredService<IValidator<AdjudicateRequest>>()));
        _ = services
            .AddScoped<EvaluateQuestionService>()
            .AddScoped<IValidator<EvaluateQuestionRequest>, EvaluateQuestionValidator>()
            .AddScoped<IService<EvaluateQuestionRequest, EvaluateQuestionResponse>>(static sp =>
                new ValidatingService<EvaluateQuestionRequest, EvaluateQuestionResponse>(
                    sp.GetRequiredService<EvaluateQuestionService>(),
                    sp.GetRequiredService<IValidator<EvaluateQuestionRequest>>()));
        _ = services
            .AddScoped<ImportTurnsService>()
            .AddScoped<IValidator<ImportTurnsRequest>, ImportTurnsValidator>()
            .AddScoped<IService<ImportTurnsRequest, ImportTurnsResponse>>(static sp =>
                new ValidatingService<ImportTurnsRequest, ImportTurnsResponse>(
                    sp.GetRequiredService<ImportTurnsService>(),
                    sp.GetRequiredService<IValidator<ImportTurnsRequest>>()));
    }
}
