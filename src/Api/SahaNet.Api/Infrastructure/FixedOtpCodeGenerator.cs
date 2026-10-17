using SahaNet.Application.Identity.Abstractions;

namespace SahaNet.Api.Infrastructure;

/// <summary>Yalnızca geliştirme ortamında kaydedilir: her OTP isteği aynı kodu üretir.</summary>
internal sealed class FixedOtpCodeGenerator(string code) : IOtpCodeGenerator
{
    public string Generate(int length) => code;
}
