using SahaNet.Domain.Identity;

namespace SahaNet.Application.Identity.Abstractions;

public interface ISmsSender
{
    Task SendAsync(PhoneNumber to, string message, CancellationToken cancellationToken);
}
