using SahaNet.Domain.Events;

namespace SahaNet.Domain.Identity;

public sealed record UserRegistered(Guid UserId) : DomainEvent;

public sealed record UserOnboarded(Guid UserId, string NicheKey, string RegionCode) : DomainEvent;

public sealed record UserDeletionRequested(Guid UserId, DateTimeOffset ScheduledFor) : DomainEvent;

/// <summary>Kalıcı silme öncesi yayınlanır; modüller kendi kişisel verilerini temizler.</summary>
public sealed record UserPurged(Guid UserId) : DomainEvent;
