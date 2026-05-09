using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;

public sealed class ChromaKnowledgeVectorStore(HttpClient httpClient, IOptions<ChromaOptions> options) : IKnowledgeVectorStore
{
    private string? collectionId;

    public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(collectionId))
        {
            return;
        }

        var chromaOptions = options.Value;
        var response = await httpClient.PostAsJsonAsync(
            BuildCollectionsPath(),
            new CreateCollectionRequest(chromaOptions.Collection, true),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var collection = await response.Content.ReadFromJsonAsync<CollectionResponse>(cancellationToken);
        collectionId = collection?.Id ?? throw new InvalidOperationException("Chroma did not return collection id.");
    }

    public async Task UpsertChunksAsync(IReadOnlyList<KnowledgeChunkRecord> chunks, CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        await EnsureCollectionAsync(cancellationToken);

        var payload = new UpsertRecordsRequest(
            chunks.Select(chunk => chunk.Id).ToArray(),
            chunks.Select(chunk => chunk.Embedding).ToArray(),
            chunks.Select(chunk => chunk.Text).ToArray(),
            chunks.Select(chunk => chunk.Metadata).ToArray());

        var response = await httpClient.PostAsJsonAsync($"{BuildCollectionPath(collectionId!)}/upsert", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private string BuildCollectionsPath()
    {
        var chromaOptions = options.Value;
        return $"/api/v2/tenants/{Uri.EscapeDataString(chromaOptions.Tenant)}/databases/{Uri.EscapeDataString(chromaOptions.Database)}/collections";
    }

    private string BuildCollectionPath(string id)
    {
        var chromaOptions = options.Value;
        return $"/api/v2/tenants/{Uri.EscapeDataString(chromaOptions.Tenant)}/databases/{Uri.EscapeDataString(chromaOptions.Database)}/collections/{Uri.EscapeDataString(id)}";
    }

    private sealed record CreateCollectionRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("get_or_create")] bool GetOrCreate);

    private sealed record CollectionResponse([property: JsonPropertyName("id")] string Id);

    private sealed record UpsertRecordsRequest(
        [property: JsonPropertyName("ids")] string[] Ids,
        [property: JsonPropertyName("embeddings")] float[][] Embeddings,
        [property: JsonPropertyName("documents")] string[] Documents,
        [property: JsonPropertyName("metadatas")] Dictionary<string, object>[] Metadatas);
}
