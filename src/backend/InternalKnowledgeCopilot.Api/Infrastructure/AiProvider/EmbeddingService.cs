using System.Security.Cryptography;
using System.Text;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public interface IEmbeddingService
{
    int Dimension { get; }

    Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}

public sealed class MockEmbeddingService : IEmbeddingService
{
    public int Dimension => 64;

    public Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var vector = new float[Dimension];
        var tokens = text.Split([' ', '\r', '\n', '\t', '.', ',', ';', ':', '-', '_', '/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token.ToLowerInvariant()));
            var index = BitConverter.ToUInt16(hash, 0) % Dimension;
            var sign = hash[2] % 2 == 0 ? 1f : -1f;
            vector[index] += sign;
        }

        var norm = MathF.Sqrt(vector.Sum(value => value * value));
        if (norm <= 0)
        {
            vector[0] = 1;
            return Task.FromResult(vector);
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }

        return Task.FromResult(vector);
    }
}

public sealed class OpenAiCompatibleEmbeddingService(OpenAiCompatibleClient client, IOptions<AiProviderOptions> options) : IEmbeddingService
{
    public int Dimension => options.Value.EmbeddingDimension;

    public Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        return client.CreateEmbeddingAsync(text, cancellationToken);
    }
}
