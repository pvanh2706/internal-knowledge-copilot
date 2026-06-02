namespace InternalKnowledgeCopilot.Api.Infrastructure.Options;

public sealed class ChromaOptions
{
    public const string SectionName = "Chroma";

    // ChromaDB tổ chức dữ liệu vector theo cấu trúc:
    // Tenant -> Database -> Collection -> Vector records.
    //
    // Ví dụ trong ứng dụng này:
    // default_tenant
    //   -> default_database
    //      -> knowledge_chunks
    //         -> các đoạn tài liệu/wiki được lưu dưới dạng embedding vector.
    //
    // Với phần lớn triển khai chỉ có một ứng dụng, có thể giữ Tenant và Database theo mặc định
    // của Chroma, sau đó dùng Collection để tách dữ liệu tri thức đã index của ứng dụng này.

    // Endpoint HTTP của ChromaDB server mà API sẽ gọi đến.
    public string BaseUrl { get; init; } = "http://localhost:8000";

    // Collection vector dùng để lưu các đoạn tri thức đã được index.
    public string Collection { get; init; } = "knowledge_chunks";

    // Namespace tenant của Chroma. Giữ mặc định nếu Chroma server không cấu hình nhiều tenant.
    public string Tenant { get; init; } = "default_tenant";

    // Namespace database bên trong tenant. Giữ mặc định nếu không dùng nhiều database Chroma.
    public string Database { get; init; } = "default_database";
}
