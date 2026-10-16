using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Features.Jobs;

/// <summary>How a job's work ended, as the definition reports it to the runner (API P6): the service's error, or its summary and result.</summary>
/// <param name="Error">The error the service returned or threw as, or null when it succeeded.</param>
/// <param name="Summary">The definition's one-line summary of the result (empty on failure: the runner uses the error's message).</param>
/// <param name="Result">The result serialized through the package's JSON options, or null.</param>
public sealed record JobWorkResult(Error? Error, string Summary, JsonElement? Result);
