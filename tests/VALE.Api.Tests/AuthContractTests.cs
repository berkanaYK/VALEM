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
    public void Registration_supports_three_explicit_login_methods()
    {
        Assert.Equal(new[] { LoginMethods.Password, LoginMethods.EmailCode, LoginMethods.Authenticator }, LoginMethods.All);
    }

    [Fact]
    public void Email_code_registration_contract_allows_no_password()
    {
        var ownerConstructor = typeof(OwnerRegisterRequest).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var ownerPassword = ownerConstructor.GetParameters()[2];
        Assert.DoesNotContain(ownerPassword.GetCustomAttributes<ValidationAttribute>(true), x => x is RequiredAttribute);
        Assert.All(ownerPassword.GetCustomAttributes<ValidationAttribute>(true), x => Assert.True(x.IsValid(null)));

        var staffConstructor = typeof(StaffRegisterRequest).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var staffPassword = staffConstructor.GetParameters()[2];
        Assert.DoesNotContain(staffPassword.GetCustomAttributes<ValidationAttribute>(true), x => x is RequiredAttribute);
        Assert.All(staffPassword.GetCustomAttributes<ValidationAttribute>(true), x => Assert.True(x.IsValid(null)));
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
