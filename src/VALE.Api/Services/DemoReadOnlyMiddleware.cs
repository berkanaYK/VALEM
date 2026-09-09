namespace VALE.Api.Services;

public sealed class DemoReadOnlyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.HasClaim("vale_demo", "true") &&
            !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await Results.Problem(statusCode: 403, title: "Deneme hesabı",
                detail: "Denemede örnek ekranları inceleyebilirsiniz. Kayıt eklemek veya değiştirmek için kendi ücretsiz hesabınızı oluşturun.").ExecuteAsync(context);
            return;
        }
        await next(context);
    }
}
