using Aw.Judge.Core.Config;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.AskQuestion;

/// <summary>
/// Asks one question: the question's config entry (enabled, threshold, providers) and the allowlist shape the chain the
/// host built, then the evaluator decides or escalates. A config that can't be read runs on the defaults (a question
/// fails open), like an invalid one.
/// </summary>
internal sealed class AskQuestionService(QuestionEvaluator evaluator, IJudgeConfigStore configs, ProviderLineup providers)
    : IService<AskQuestionRequest, AskQuestionResponse>
{
    public async ValueTask<Result<AskQuestionResponse>> Handle(AskQuestionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reading = ConfigReading.From(await configs.Load(cancellationToken).ConfigureAwait(false));
        var (config, warning) = reading.IsSuccess ? (reading.Value.Config, reading.Value.Warning) : (JudgeConfig.Default, reading.Error.Message);
        var context = new EvaluationContext
        {
            Providers = providers.Providers,
            Chain = request.AllowedProviders is { } allowed ? providers.Chain.Restrict(allowed) : providers.Chain,
            Override = QuestionOverride.For(config, request.Question),
            SessionId = request.SessionId,
        };
        var outcome = await evaluator.Ask(request.Question, request.Input, context, cancellationToken).ConfigureAwait(false);
        return new AskQuestionResponse(outcome, warning);
    }
}
