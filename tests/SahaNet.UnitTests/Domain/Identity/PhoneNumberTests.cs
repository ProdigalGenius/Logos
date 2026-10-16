using SahaNet.Domain.Identity;

namespace SahaNet.UnitTests.Domain.Identity;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("05321234567")]
    [InlineData("5321234567")]
    [InlineData("+90 532 123 45 67")]
    [InlineData("90 (532) 123-45-67")]
    public void Create_NormalizesToE164(string input)
    {
        var result = PhoneNumber.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("+905321234567");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("02121234567")]
    [InlineData("53212345")]
    [InlineData("+44 7700 900123")]
    public void Create_RejectsInvalid(string? input)
    {
        var result = PhoneNumber.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.PhoneInvalid);
    }

    [Fact]
    public void ToString_IsMasked()
    {
        var phone = PhoneNumber.Create("05321234567").Value;

        phone.ToString().Should().Be("+90 5** *** ** 67").And.NotContain("123");
    }
}
