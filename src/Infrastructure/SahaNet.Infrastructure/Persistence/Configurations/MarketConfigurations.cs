using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Market;

namespace SahaNet.Infrastructure.Persistence.Configurations;

internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("listings", SahaNetDbContext.MarketSchema);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.NicheKey).HasMaxLength(32).IsRequired();
        builder.Property(l => l.CategoryKey).HasMaxLength(64).IsRequired();
        builder.Property(l => l.Title).HasMaxLength(Listing.TitleMaxLength).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(Listing.DescriptionMaxLength);
        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.UnitPrice).HasPrecision(14, 2);
        builder.Property(l => l.Quantity).HasPrecision(14, 3);
        builder.Property(l => l.RemainingQuantity).HasPrecision(14, 3);
        builder.Property(l => l.MinBidIncrement).HasPrecision(14, 2);
        builder.Property(l => l.ReservePrice).HasPrecision(14, 2);
        builder.Property(l => l.Unit).HasMaxLength(16).IsRequired();
        builder.Property(l => l.RegionCode).HasMaxLength(2);
        builder.Property(l => l.ModerationNote).HasMaxLength(500);

        // Kategoriye özel alanlar jsonb olarak saklanır (şema modülde tanımlı).
        builder.Property(l => l.Attributes)
            .HasColumnType("jsonb")
            .HasConversion(
                d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null),
                s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
                    d => d.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key, kv.Value)),
                    d => new Dictionary<string, string>(d)));

        // Eşzamanlı teklifler için PostgreSQL xmin satır sürümü.
        builder.Property<uint>("RowVersion").IsRowVersion();

        builder.HasIndex(l => new { l.Status, l.NicheKey, l.CategoryKey, l.CreatedAt });
        builder.HasIndex(l => new { l.Status, l.ExpiresAt });
        builder.HasIndex(l => l.SellerId);
        builder.Ignore(l => l.HighestBid);
        builder.Ignore(l => l.DomainEvents);

        builder.HasMany(l => l.Offers).WithOne().HasForeignKey(o => o.ListingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.Offers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<User>().WithMany().HasForeignKey(l => l.SellerId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("offers", SahaNetDbContext.MarketSchema);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.UnitPrice).HasPrecision(14, 2);
        builder.Property(o => o.Quantity).HasPrecision(14, 3);
        builder.Property(o => o.Message).HasMaxLength(Offer.MessageMaxLength);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(o => o.Total);
        builder.HasIndex(o => o.BuyerId);

        builder.HasOne<User>().WithMany().HasForeignKey(o => o.BuyerId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations", SahaNetDbContext.MarketSchema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => c.ListingId);
        builder.Ignore(c => c.DomainEvents);

        builder.OwnsMany(c => c.Participants, p =>
        {
            p.ToTable("conversation_participants", SahaNetDbContext.MarketSchema);
            p.WithOwner().HasForeignKey(x => x.ConversationId);
            p.HasKey(x => new { x.ConversationId, x.UserId });
            p.HasIndex(x => x.UserId);
            p.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("messages", SahaNetDbContext.MarketSchema);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.Text).HasMaxLength(ChatMessage.TextMaxLength);
        builder.Property(m => m.MediaUrl).HasMaxLength(1024);
        builder.HasIndex(m => new { m.ConversationId, m.SentAt });
        builder.HasIndex(m => new { m.SenderId, m.SentAt });

        builder.HasOne<Conversation>().WithMany().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Cascade);
    }
}
