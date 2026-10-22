namespace Aw.CodeIndex.Infrastructure.Embedding;

/// <summary>An embedding model and the digest the build expects it to have (the 'ollama-model' row of context/code-index.lock).</summary>
/// <param name="Name">The model name as Ollama lists it ('nomic-embed-text:latest').</param>
/// <param name="Digest">The manifest digest, 'sha256:' and 64 hex digits.</param>
internal sealed record ModelPin(string Name, string Digest);
