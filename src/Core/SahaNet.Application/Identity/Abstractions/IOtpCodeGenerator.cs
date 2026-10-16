namespace SahaNet.Application.Identity.Abstractions;

public interface IOtpCodeGenerator
{
    string Generate(int length);
}
