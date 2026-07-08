using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;

public interface IApiKeyProtector
{
    string? Protect(string? plaintext);

    string? Unprotect(string? stored);

    bool IsProtected(string? stored);
}

// Mã hóa API key trước khi lưu SQLite để người đọc được file DB không lấy được key nhà cung cấp.
// Giá trị đã mã hóa mang prefix để phân biệt với bản ghi plaintext cũ và cho phép migrate dần.
public sealed class DataProtectionApiKeyProtector(IDataProtectionProvider dataProtectionProvider) : IApiKeyProtector
{
    public const string ProtectedPrefix = "enc:v1:";

    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("AiProviderSettings.ApiKey");

    public string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || IsProtected(plaintext))
        {
            return plaintext;
        }

        return ProtectedPrefix + protector.Protect(plaintext);
    }

    public string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || !IsProtected(stored))
        {
            // Bản ghi cũ chưa mã hóa: trả nguyên trạng, sẽ được mã hóa lại khi khởi động hoặc khi lưu.
            return stored;
        }

        return protector.Unprotect(stored[ProtectedPrefix.Length..]);
    }

    public bool IsProtected(string? stored)
    {
        return stored is not null && stored.StartsWith(ProtectedPrefix, StringComparison.Ordinal);
    }
}

// Mã hóa lại các API key còn ở dạng plaintext từ trước khi bật mã hóa. Idempotent, chạy mỗi lần khởi động.
public static class AiProviderKeyStartupMigration
{
    public static async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IApiKeyProtector>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AiProviderKeyStartupMigration");

        try
        {
            var settings = await dbContext.AiProviderSettings.ToListAsync(cancellationToken);
            var changedCount = 0;
            foreach (var setting in settings)
            {
                if (!string.IsNullOrEmpty(setting.ApiKey) && !protector.IsProtected(setting.ApiKey))
                {
                    setting.ApiKey = protector.Protect(setting.ApiKey);
                    changedCount++;
                }

                if (!string.IsNullOrEmpty(setting.EmbeddingApiKey) && !protector.IsProtected(setting.EmbeddingApiKey))
                {
                    setting.EmbeddingApiKey = protector.Protect(setting.EmbeddingApiKey);
                    changedCount++;
                }
            }

            if (changedCount > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Đã mã hóa lại {Count} API key plaintext trong ai_provider_settings.", changedCount);
            }
        }
        catch (Exception ex)
        {
            // Không chặn khởi động nếu bảng chưa sẵn sàng (ví dụ chưa chạy migration ở lần cài mới).
            logger.LogWarning(ex, "Bỏ qua bước mã hóa lại API key vì chưa truy cập được bảng ai_provider_settings.");
        }
    }
}
