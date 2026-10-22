namespace Aw.CodeIndex.Core.Model;

/// <summary>The embeddings layer's settings (spec §13): loopback only, never a cloud model.</summary>
/// <param name="Enabled">False leaves the layer disabled.</param>
/// <param name="Url">The server, 'http://127.0.0.1:11434' or '[::1]', no credentials, never 'localhost'.</param>
/// <param name="Model">The model name, or null for the lock's model.</param>
public sealed record EmbeddingsSettings(bool Enabled, string Url, string? Model);
