using System.Net.Http.Json;
using System.Text.Json;
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

    public async Task<IReadOnlyList<KnowledgeVectorSearchResult>> QueryAsync(float[] embedding, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        var payload = new QueryRecordsRequest(
            [embedding],
            Math.Max(1, limit),
            ["documents", "metadatas", "distances"]);

        var response = await httpClient.PostAsJsonAsync($"{BuildCollectionPath(collectionId!)}/query", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var ids = ReadNestedStrings(root, "ids").FirstOrDefault() ?? [];
        var texts = ReadNestedStrings(root, "documents").FirstOrDefault() ?? [];
        var distances = ReadNestedDoubles(root, "distances").FirstOrDefault() ?? [];
        var metadatas = ReadNestedMetadata(root, "metadatas").FirstOrDefault() ?? [];

        var count = new[] { ids.Count, texts.Count, metadatas.Count }
            .Where(value => value > 0)
            .DefaultIfEmpty(0)
            .Min();

        var results = new List<KnowledgeVectorSearchResult>(count);
        for (var i = 0; i < count; i++)
        {
            results.Add(new KnowledgeVectorSearchResult(
                ids[i],
                texts[i],
                metadatas[i],
                i < distances.Count ? distances[i] : null));
        }

        return results;
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

    private sealed record QueryRecordsRequest(
        [property: JsonPropertyName("query_embeddings")] float[][] QueryEmbeddings,
        [property: JsonPropertyName("n_results")] int NResults,
        [property: JsonPropertyName("include")] string[] Include);

    private static List<List<string>> ReadNestedStrings(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Select(items => items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToList()
                : new List<string>())
            .ToList();
    }

    private static List<List<double>> ReadNestedDoubles(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Select(items => items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().Select(item => item.TryGetDouble(out var value) ? value : 0).ToList()
                : new List<double>())
            .ToList();
    }

    private static List<List<IReadOnlyDictionary<string, object?>>> ReadNestedMetadata(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Select(items => items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().Select(ReadMetadata).ToList()
                : new List<IReadOnlyDictionary<string, object?>>())
            .ToList();
    }

    private static IReadOnlyDictionary<string, object?> ReadMetadata(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object?>();
        }

        return element.EnumerateObject()
            .ToDictionary(property => property.Name, property => ReadMetadataValue(property.Value));
    }

    private static object? ReadMetadataValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
