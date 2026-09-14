namespace VALE.Mobile;

public sealed class UserFacingException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

public static class UserMessages
{
    public static string For(Exception exception) => exception switch
    {
        UserFacingException => exception.Message,
        TwoFactorRequiredException => "İki adımlı doğrulama kodunuzu girin.",
        PermissionException => "Bu işlem için telefonunuzdan izin vermeniz gerekiyor. Uygulama izinlerini ayarlardan kontrol edin.",
        FeatureNotSupportedException => "Telefonunuz bu özelliği desteklemiyor. Varsa diğer seçeneği kullanın.",
        OperationCanceledException => "İşlem iptal edildi veya bekleme süresi doldu. Yeniden deneyebilirsiniz.",
        System.Text.Json.JsonException => "Sunucudan gelen yanıt okunamadı. Uygulamayı güncelleyip yeniden deneyin.",
        IOException => "Dosya okunamadı veya kaydedilemedi. Dosyayı ve telefonunuzdaki boş alanı kontrol edin.",
        HttpRequestException => "İnternet bağlantınızı kontrol edip tekrar deneyin.",
        _ => "İşlem tamamlanamadı. Tekrar deneyin; sorun sürerse İletişim ve Destek bölümünden hangi ekranda olduğunu bildirin."
    };
}
