using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Identity;

namespace SahaNet.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration(IPersonalDataProtector protector) : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", SahaNetDbContext.IdentitySchema);
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        // KVKK: telefon şifreli saklanır, arama HMAC özetiyle yapılır.
        builder.Property(u => u.Phone)
            .HasColumnName("phone_encrypted")
            .HasConversion(
                phone => protector.Protect(phone.Value),
                stored => PhoneNumber.FromTrusted(protector.Unprotect(stored)))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.PhoneHash).HasColumnName("phone_hash").HasMaxLength(64).IsRequired();
        builder.HasIndex(u => u.PhoneHash).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength);
        builder.Property(u => u.NicheKey).HasMaxLength(32);
        builder.Property(u => u.RegionCode).HasMaxLength(2);
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(u => new { u.Status, u.DeletionRequestedAt });

        builder.Ignore(u => u.IsOnboarded);
        builder.Ignore(u => u.DomainEvents);
    }
}
