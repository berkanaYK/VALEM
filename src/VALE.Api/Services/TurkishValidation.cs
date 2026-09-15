using Microsoft.AspNetCore.Mvc;

namespace VALE.Api.Services;

public static class TurkishValidation
{
    public static IActionResult Response(ActionContext context)
    {
        var errors = context.ModelState.Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(x => x.Key, x => new[] { Message(x.Key) });
        return new BadRequestObjectResult(new ValidationProblemDetails(errors)
        {
            Status = 400,
            Title = "Bilgilerinizi kontrol edin",
            Detail = "Bazı alanlar eksik veya uygun biçimde değil. İşaretlenen bilgileri düzeltip tekrar deneyin.",
            Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
        });
    }

    private static string Message(string field) => field.Split('.').Last().ToLowerInvariant() switch
    {
        "email" => "Geçerli bir e-posta adresi yazın.",
        "username" => "Kullanıcı adı 3-30 karakter olmalı; yalnızca harf, rakam, nokta, alt çizgi veya kısa çizgi içermelidir.",
        "fullname" => "Ad soyad 2 ile 120 karakter arasında olmalı.",
        "password" or "newpassword" => "Parola alanını kontrol edin. 6-20 karakter, büyük/küçük harf, rakam ve özel karakter kullanın.",
        "code" or "twofactorcode" => "6 haneli doğrulama kodunu yazın.",
        "birthdate" => "Geçerli bir doğum tarihi seçin veya bu alanı boş bırakın.",
        "phonenumber" => "Telefon numarası en fazla 30 karakter olmalı.",
        "city" => "Şehir adı en fazla 80 karakter olmalı.",
        "about" => "Hakkımda alanı en fazla 300 karakter olmalı.",
        "base64data" => "Fotoğraf çok büyük veya okunamadı. Daha küçük bir fotoğraf seçin.",
        "hourlyrate" or "amount" => "Geçerli, pozitif bir tutar yazın.",
        "licenseplate" => "Plakayı 3 ile 16 karakter arasında yazın.",
        _ => "Bu alanı kontrol edin; gerekli bilgiyi uygun uzunlukta ve biçimde yazın."
    };
}
