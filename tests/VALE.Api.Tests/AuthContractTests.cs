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
