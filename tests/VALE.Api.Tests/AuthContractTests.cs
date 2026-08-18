using System.ComponentModel.DataAnnotations;
using VALE.Contracts;

namespace VALE.Api.Tests;

public sealed class AuthContractTests
{
    [Fact]
    public void Two_factor_code_requires_six_digits()
    {
        var invalid = new TwoFactorCodeRequest("12345");
        var context = new ValidationContext(invalid);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(invalid, context, results, validateAllProperties: true));
    }

    [Fact]
    public void Profile_color_requires_hex_rgb()
    {
        var invalid = new UpdateAccountProfileRequest("Test User", null, "System", "Blue", "blue");
        var context = new ValidationContext(invalid);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(invalid, context, results, validateAllProperties: true));
    }

    [Fact]
    public void Ticket_delete_requires_reason()
    {
        var invalid = new DeleteTicketRequest("x");
        var context = new ValidationContext(invalid);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(invalid, context, results, validateAllProperties: true));
    }
}
