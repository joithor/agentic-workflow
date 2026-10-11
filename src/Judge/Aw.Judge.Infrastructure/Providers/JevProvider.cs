using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Secrets;
using Aw.Kernel.Json;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// Jev, the TypeSafe API (POST https://api.typesafe.ai/v1/systemone), for the text classes: one 'choice' question per
/// call whose options are the question's outputs, each with its criterion (or its own name). The API key comes from
/// the secret source; without one Jev is unavailable ('no-api-key'), which is not a failure. A non-success status is
/// errored ('http-NNN'); past the budget it is unavailable ('timeout'); unreachable is unavailable ('network-error').
/// </summary>
/// <param name="http">Sends the request (owned: disposed with the provider).</param>
/// <param name="secrets">Reads the API key.</param>
internal sealed class JevProvider(HttpClient http, ISecretSource secrets) : IModelProvider, IDisposable
{
    /// <summary>The model.</summary>
    public const string Model = "jev-1.13.0";

    /// <summary>The API's choice-option limit.</summary>
    public const int MaxOptions = 255;

    private const string QuestionId = "decision";

    private static readonly Uri _endpoint = new("https://api.typesafe.ai/v1/systemone");

    /// <inheritdoc />
    public ProviderName Name => ProviderName.Jev;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass>(ContentClasses.Text);

    /// <inheritdoc />
    public async ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        if (question.Outputs.Count > MaxOptions)
        {
            return new ProviderErrored(ProviderReasons.TooManyOptions);
        }

        if (await secrets.Read(SecretName.TypeSafeApiKey, cancellationToken).ConfigureAwait(false) is not { } key)
        {
            return new ProviderUnavailable(ProviderReasons.NoApiKey);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(budget);
        try
        {
            using var request = Request(question, input, key);
            using var response = await http.SendAsync(request, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ProviderErrored(ProviderReasons.Http((int)response.StatusCode));
            }

            var body = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
            return Read(body, question);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new ProviderUnavailable(ProviderUnavailable.Timeout);
        }
        catch (HttpRequestException)
        {
            return new ProviderUnavailable(ProviderReasons.NetworkError);
        }
    }

    /// <inheritdoc />
    public void Dispose() => http.Dispose();

    private static HttpRequestMessage Request(QuestionRef question, JsonNode? input, string key)
    {
        var criteria = question.Outputs.ToDictionary(
            option => option,
            option => question.Criteria?.GetValueOrDefault(option) ?? option,
            StringComparer.Ordinal);
        var body = new JevRequest(
            input?.DeepClone(),
            Model,
            new Dictionary<string, JevChoiceQuestion>(StringComparer.Ordinal) { [QuestionId] = new("choice", question.Prompt, criteria) });
        var content = new ByteArrayContent(AwJson.Serialize(body, WireJson.Options));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private static ProviderResult Read(byte[] body, QuestionRef question)
    {
        var response = AwJson.Deserialize<JevResponse>(body, WireJson.Options);
        if (response.IsError || response.Value.Answers?.GetValueOrDefault(QuestionId) is not { Type: "choice", Choice: { } choice } answer)
        {
            return new ProviderErrored(ProviderReasons.UnparseableAnswer);
        }

        if (!question.Outputs.Contains(choice, StringComparer.Ordinal))
        {
            return new ProviderErrored(ProviderReasons.OutOfEnum);
        }

        var confidence = answer.Confidence is >= 0 and <= 1 ? answer.Confidence.Value : AgentAnswer.DefaultConfidence;
        return new ProviderDecided(choice, confidence, ProviderReasons.Model) { Probabilities = answer.Probabilities };
    }
}
