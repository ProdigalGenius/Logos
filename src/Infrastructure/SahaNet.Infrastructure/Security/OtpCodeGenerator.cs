using System.Globalization;
using System.Security.Cryptography;
using SahaNet.Application.Identity.Abstractions;

namespace SahaNet.Infrastructure.Security;

internal sealed class OtpCodeGenerator : IOtpCodeGenerator
{
    public string Generate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 4);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, 9);

        var max = (int)Math.Pow(10, length);
        return RandomNumberGenerator.GetInt32(0, max).ToString("D" + length.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
