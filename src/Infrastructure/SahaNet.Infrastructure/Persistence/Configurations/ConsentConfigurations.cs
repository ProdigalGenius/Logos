using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaNet.Domain.Consents;
using SahaNet.Domain.Identity;

namespace SahaNet.Infrastructure.Persistence.Configurations;

internal sealed class ConsentDocumentConfiguration : IEntityTypeConfiguration<ConsentDocument>
{
    public void Configure(EntityTypeBuilder<ConsentDocument> builder)
    {
        builder.ToTable("consent_documents", SahaNetDbContext.IdentitySchema);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.Version).HasMaxLength(16).IsRequired();
        builder.Property(d => d.Title).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Content).IsRequired();
        builder.HasIndex(d => new { d.Type, d.Version }).IsUnique();

        builder.HasData(ConsentDocumentSeed.All);
    }
}

internal sealed class UserConsentConfiguration : IEntityTypeConfiguration<UserConsent>
{
    public void Configure(EntityTypeBuilder<UserConsent> builder)
    {
        builder.ToTable("user_consents", SahaNetDbContext.IdentitySchema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.Version).HasMaxLength(16).IsRequired();
        builder.Property(c => c.Source).HasMaxLength(16).IsRequired();
        builder.HasIndex(c => new { c.UserId, c.RecordedAt });

        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ConsentDocument>().WithMany().HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
