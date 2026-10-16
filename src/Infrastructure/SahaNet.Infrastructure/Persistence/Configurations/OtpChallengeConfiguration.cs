using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaNet.Domain.Identity;

namespace SahaNet.Infrastructure.Persistence.Configurations;

internal sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("otp_challenges", SahaNetDbContext.IdentitySchema);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.PhoneHash).HasMaxLength(64).IsRequired();
        builder.Property(o => o.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(o => o.FailedAttempts).IsConcurrencyToken();
        builder.HasIndex(o => new { o.PhoneHash, o.CreatedAt });
    }
}
