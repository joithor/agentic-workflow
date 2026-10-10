using Aw.Judge.Features.SetConfig;

namespace Aw.Judge.Testing;

/// <summary>Builds a SetConfigRequest with deterministic defaults: wake-gate's threshold to 0.85.</summary>
public sealed class SetConfigRequestBuilder
{
    private SetConfigRequest _request = new("wake-gate", "threshold", "0.85");

    /// <summary>Sets the question, field and value.</summary>
    /// <param name="question">The question.</param>
    /// <param name="field">The field.</param>
    /// <param name="value">The value.</param>
    /// <returns>This builder.</returns>
    public SetConfigRequestBuilder Setting(string question, string field, string value)
    {
        _request = new SetConfigRequest(question, field, value);
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SetConfigRequest Build() => _request;
}
