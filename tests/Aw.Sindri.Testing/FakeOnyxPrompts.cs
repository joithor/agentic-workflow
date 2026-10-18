using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>Prompts answered from a script; remembers what it was asked (never the answers).</summary>
public sealed class FakeOnyxPrompts : IOnyxPrompts
{
    private readonly Queue<string?> _answers = new();

    /// <summary>Gets or sets a value indicating whether a person is there to answer.</summary>
    public bool Interactive { get; set; } = true;

    /// <summary>Gets the questions asked (and pauses shown), in order.</summary>
    public IList<string> Asked { get; } = [];

    /// <summary>Gets the number of times a secret was asked for.</summary>
    public int SecretsAsked { get; private set; }

    /// <summary>Gets or sets what runs when a pause is shown (a test looks at the world while the owner is "at the prompt").</summary>
    public Action? OnPause { get; set; }

    /// <summary>Queues the next answers, for Ask and AskSecret in the order they are asked.</summary>
    /// <param name="answers">The answers; null ends input.</param>
    public void Answer(params string?[] answers)
    {
        foreach (var answer in answers)
        {
            _answers.Enqueue(answer);
        }
    }

    /// <inheritdoc />
    public ValueTask Pause(string message, CancellationToken cancellationToken)
    {
        Asked.Add(message);
        OnPause?.Invoke();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<string?> Ask(string question, CancellationToken cancellationToken)
    {
        Asked.Add(question);
        return ValueTask.FromResult(_answers.Count > 0 ? _answers.Dequeue() : null);
    }

    /// <inheritdoc />
    public ValueTask<string?> AskSecret(string question, CancellationToken cancellationToken)
    {
        Asked.Add(question);
        SecretsAsked++;
        return ValueTask.FromResult(_answers.Count > 0 ? _answers.Dequeue() : null);
    }
}
