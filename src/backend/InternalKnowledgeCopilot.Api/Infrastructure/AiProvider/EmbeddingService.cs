using System.Security.Cryptography;
using System.Text;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public interface IEmbeddingService
{
    int Dimension { get; }

    float[] CreateEmbedding(string text);
}

public sealed class MockEmbeddingService : IEmbeddingService
{
    public int Dimension => 64;

    public float[] CreateEmbedding(string text)
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
            return vector;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }

        return vector;
    }
}
