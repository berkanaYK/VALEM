using Microsoft.AspNetCore.Diagnostics;

namespace VALE.Api.Services;

public sealed class ApiException(int statusCode, string title, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ApiException apiException)
        {
            logger.LogWarning(
                "VALE API isteği {StatusCode} ile reddedildi: {Message}",
                apiException.StatusCode,
                apiException.Message);

            httpContext.Response.StatusCode = apiException.StatusCode;
            await Results.Problem(
                    statusCode: apiException.StatusCode,
                    title: apiException.Title,
                    detail: apiException.Message)
                .ExecuteAsync(httpContext);
            return true;
        }

        logger.LogError(
            exception,
            "VALE API beklenmeyen bir hata üretti. TraceIdentifier: {TraceIdentifier}",
            httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Sunucu hatası",
                detail: "İşlem sırasında beklenmeyen bir sunucu hatası oluştu. Lütfen tekrar deneyin.")
            .ExecuteAsync(httpContext);
        return true;
    }
}
