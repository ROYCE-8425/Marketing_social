using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class PhoneAndEmailValidationTests
{
    [Theory]
    [InlineData("0901234567", "0901234567")]
    [InlineData("+84901234567", "0901234567")]
    [InlineData("84901234567", "0901234567")]
    [InlineData("090 123 4567", "0901234567")]
    [InlineData("(0901) 234-567", "0901234567")]
    [InlineData("0321234567", "0321234567")]
    [InlineData("+84 38 123 4567", "0381234567")]
    public void PhoneNormalizer_ValidVnPhone_NormalizesSuccessfully(string input, string expected)
    {
        var result = PhoneNormalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abcdefg")]
    [InlineData("0123456789012")]
    [InlineData("+1234567890")]
    public void PhoneNormalizer_InvalidPhone_ThrowsInvalidPhone(string input)
    {
        var ex = Assert.Throws<DomainRuleException>(() => PhoneNormalizer.Normalize(input));
        Assert.Equal("InvalidPhone", ex.Code);
    }

    [Theory]
    [InlineData("user@domain.com", "user@domain.com")]
    [InlineData("USER@DOMAIN.VN", "user@domain.vn")]
    [InlineData("  contact@hoc-cung-royce.edu.vn  ", "contact@hoc-cung-royce.edu.vn")]
    public void EmailValidator_ValidEmail_NormalizesSuccessfully(string input, string expected)
    {
        var result = EmailValidator.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("user@")]
    [InlineData("@domain.com")]
    [InlineData("user@domain")] // Missing dot in domain
    [InlineData("user@domain.")] // Trailing dot
    public void EmailValidator_InvalidEmail_ThrowsInvalidEmail(string input)
    {
        var ex = Assert.Throws<DomainRuleException>(() => EmailValidator.Normalize(input));
        Assert.Equal("InvalidEmail", ex.Code);
    }
}
