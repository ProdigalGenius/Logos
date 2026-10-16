using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SahaNet.Domain.Identity;
using SahaNet.Infrastructure.Options;
using SahaNet.Infrastructure.Security;
using SahaNet.UnitTests.Fakes;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SahaNet.UnitTests.Infrastructure;

public class SecurityTests
{
    private static AesGcmPersonalDataProtector NewProtector() => new(MsOptions.Create(new PersonalDataOptions
    {
        EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        LookupKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    }));

    [Fact]
    public void Protector_RoundTrips_WithRandomNonce()
    {
        using var protector = NewProtector();

        var a = protector.Protect("+905321234567");
        var b = protector.Protect("+905321234567");

        a.Should().NotBe(b).And.NotContain("5321234567");
        protector.Unprotect(a).Should().Be("+905321234567");
    }

    [Fact]
    public void Protector_DetectsTampering()
    {
        using var protector = NewProtector();
        var bytes = Convert.FromBase64String(protector.Protect("gizli")[3..]);
        bytes[^1] ^= 0xFF;

        var act = () => protector.Unprotect("v1:" + Convert.ToBase64String(bytes));

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Protector_LookupHash_IsDeterministic()
    {
        using var protector = NewProtector();

        protector.ComputeLookupHash("x").Should().Be(protector.ComputeLookupHash("x")).And.HaveLength(64);
        protector.ComputeLookupHash("x").Should().NotBe(protector.ComputeLookupHash("y"));
    }

    [Fact]
    public void Protector_RejectsShortKey()
    {
        var act = () => new AesGcmPersonalDataProtector(MsOptions.Create(new PersonalDataOptions
        {
            EncryptionKey = Convert.ToBase64String(new byte[16]),
            LookupKey = Convert.ToBase64String(new byte[32]),
        }));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OtpGenerator_ProducesDigitsOfRequestedLength()
    {
        var generator = new OtpCodeGenerator();

        for (var i = 0; i < 50; i++)
        {
            generator.Generate(6).Should().MatchRegex("^[0-9]{6}$");
        }
    }

    [Fact]
    public async Task JwtTokenService_IssuesValidTokenWithSubject()
    {
        var settings = new JwtOptions { Issuer = "sahanet", Audience = "mobile", SigningKey = new string('k', 48) };
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var service = new JwtTokenService(MsOptions.Create(settings), clock);
        var user = User.Register(PhoneNumber.Create("05321234567").Value, "hash", clock.UtcNow);

        var token = service.CreateAccessToken(user);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = "sahanet",
            ValidAudience = "mobile",
            IssuerSigningKey = JwtTokenService.CreateSigningKey(settings.SigningKey),
        });

        validation.IsValid.Should().BeTrue();
        validation.Claims[JwtRegisteredClaimNames.Sub].Should().Be(user.Id.ToString());
        token.Token.Should().NotContain("5321234567");
        service.GenerateRefreshToken().Should().NotBe(service.GenerateRefreshToken());
    }
}
