using Microsoft.AspNetCore.Identity;

namespace VALE.Api.Services;

public sealed class TurkishIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError Error(string code, string text) => new() { Code = code, Description = text };
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "İşlem tamamlanamadı. Lütfen tekrar deneyin.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Bu bilgi başka bir işlemde değişti. Sayfayı yenileyip tekrar deneyin.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "Mevcut parola doğru değil.");
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "Doğrulama bağlantısı veya kodu geçersiz ya da süresi dolmuş. Yenisini isteyin.");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), "Bu e-posta adresi zaten kullanılıyor. Giriş yapın veya parolanızı yenileyin.");
    public override IdentityError DuplicateUserName(string userName) => DuplicateEmail(userName);
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "Geçerli bir e-posta adresi yazın.");
    public override IdentityError InvalidUserName(string? userName) => InvalidEmail(userName);
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"Parolanız en az {length} karakter olmalı.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "Parolanıza en az bir rakam ekleyin.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "Parolanıza en az bir küçük harf ekleyin.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "Parolanıza en az bir büyük harf ekleyin.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "Parolanıza en az bir özel karakter ekleyin (örneğin ! veya ?).");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"Parolanızda en az {uniqueChars} farklı karakter kullanın.");
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword), "Hesabınızda zaten bir parola var. Parola değiştirme ekranını kullanın.");
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), "Kullanıcı zaten bu yetkiye sahip.");
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), "Kullanıcının bu yetkisi bulunmuyor.");
}
