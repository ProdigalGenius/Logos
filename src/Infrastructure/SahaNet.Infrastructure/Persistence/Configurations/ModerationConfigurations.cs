using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaNet.Domain.Moderation;

namespace SahaNet.Infrastructure.Persistence.Configurations;

internal sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> builder)
    {
        builder.ToTable("reports", SahaNetDbContext.ModerationSchema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.TargetType).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(24);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Action).HasConversion<string>().HasMaxLength(24);
        builder.Property(r => r.Note).HasMaxLength(ContentReport.NoteMaxLength);
        builder.Property(r => r.ResolutionNote).HasMaxLength(ContentReport.NoteMaxLength);
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
        builder.HasIndex(r => new { r.TargetType, r.TargetId, r.Status });
        builder.HasIndex(r => new { r.ReporterId, r.CreatedAt });
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_log", SahaNetDbContext.ModerationSchema);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.TargetType).HasMaxLength(32).IsRequired();
        builder.Property(a => a.TargetId).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.HasIndex(a => a.At);
    }
}
