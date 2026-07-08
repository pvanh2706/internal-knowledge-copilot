using Microsoft.AspNetCore.Diagnostics;

namespace InternalKnowledgeCopilot.Api.Infrastructure.ErrorHandling;

// Chốt chặn cuối: mọi exception chưa được controller xử lý đều trả về một schema lỗi thống nhất,
// không bao giờ lộ stack trace ra ngoài. Chi tiết lỗi chỉ ghi vào log server kèm traceId để tra cứu.
public sealed class ApiErrorExceptionHandler(ILogger<ApiErrorExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;
        logger.LogError(
            exception,
            "Lỗi chưa được xử lý tại {Method} {Path}. TraceId: {TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                error = "internal_error",
                message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại hoặc liên hệ quản trị viên.",
                traceId,
            },
            cancellationToken);

        return true;
    }
}
