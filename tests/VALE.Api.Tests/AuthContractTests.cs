using System.ComponentModel.DataAnnotations;
using System.Reflection;
using VALE.Contracts;
using Xunit;

namespace VALE.Api.Tests;

public sealed class AuthContractTests
{
    [Fact]
    public void Two_factor_code_requires_six_digits()
    {
        Assert.False(ConstructorParameterIsValid<TwoFactorCodeRequest>(0, "12345"));
        Assert.True(ConstructorParameterIsValid<TwoFactorCodeRequest>(0, "123456"));
    }

    [Fact]
    public void Authenticator_login_is_passwordless_and_requires_email_and_code()
    {
        var parameters = typeof(TwoFactorLoginRequest).GetConstructors().Single().GetParameters();
        Assert.DoesNotContain(parameters, x => x.Name!.Equals("password", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Email", parameters[0].Name, ignoreCase: true);
        Assert.Equal("Code", parameters[1].Name, ignoreCase: true);
        Assert.False(ConstructorParameterIsValid<TwoFactorLoginRequest>(1, "12345"));
        Assert.True(ConstructorParameterIsValid<TwoFactorLoginRequest>(1, "123456"));
    }

    [Fact]
    public void Registration_supports_three_explicit_login_methods()
    {
        Assert.Equal(new[] { LoginMethods.Password, LoginMethods.EmailCode, LoginMethods.Authenticator }, LoginMethods.All);
    }

    [Fact]
    public void Registration_requires_a_bounded_password_and_validates_username()
    {
        var ownerConstructor = typeof(OwnerRegisterRequest).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var ownerPassword = ownerConstructor.GetParameters()[2];
        Assert.Contains(ownerPassword.GetCustomAttributes<ValidationAttribute>(true), x => x is RequiredAttribute);
        Assert.False(ownerPassword.GetCustomAttributes<ValidationAttribute>(true).All(x => x.IsValid(null)));
        Assert.True(ownerPassword.GetCustomAttributes<ValidationAttribute>(true).All(x => x.IsValid("Test!1")));
        Assert.False(ownerPassword.GetCustomAttributes<ValidationAttribute>(true).All(x => x.IsValid("Test!1234567890123456")));

        var staffConstructor = typeof(StaffRegisterRequest).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var staffPassword = staffConstructor.GetParameters()[2];
        Assert.Contains(staffPassword.GetCustomAttributes<ValidationAttribute>(true), x => x is RequiredAttribute);
        Assert.False(staffPassword.GetCustomAttributes<ValidationAttribute>(true).All(x => x.IsValid(null)));

        var username = ownerConstructor.GetParameters().Single(x => x.Name!.Equals("Username", StringComparison.OrdinalIgnoreCase));
        var usernameRules = username.GetCustomAttributes<ValidationAttribute>(true).ToArray();
        Assert.True(usernameRules.All(x => x.IsValid("berkan_01")));
        Assert.False(usernameRules.All(x => x.IsValid("geçersiz kullanıcı")));
    }

    [Fact]
    public void Profile_color_requires_hex_rgb()
    {
        Assert.False(ConstructorParameterIsValid<UpdateAccountProfileRequest>(4, "blue"));
        Assert.True(ConstructorParameterIsValid<UpdateAccountProfileRequest>(4, "#2563EB"));
    }

    [Fact]
    public void Ticket_delete_requires_reason()
    {
        Assert.False(ConstructorParameterIsValid<DeleteTicketRequest>(0, "x"));
        Assert.True(ConstructorParameterIsValid<DeleteTicketRequest>(0, "Yanlış plaka ile açıldı"));
    }

    private static bool ConstructorParameterIsValid<T>(int parameterIndex, object? value)
    {
        var constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var parameter = constructor.GetParameters()[parameterIndex];
        var attributes = parameter.GetCustomAttributes<ValidationAttribute>(inherit: true).ToArray();
        Assert.NotEmpty(attributes);
        return attributes.All(attribute => attribute.IsValid(value));
    }
}
