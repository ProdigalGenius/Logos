using SahaNet.Application.Abstractions;

namespace SahaNet.Infrastructure;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
