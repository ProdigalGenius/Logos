using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaNet.Domain.Payments;

namespace SahaNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mali kayıt: kullanıcı/ilan tablolarına FK verilmez; kullanıcı silinse de (VUK saklama yükümlülüğü)
/// kayıt kalır ve kişiyi yalnızca anlamsız kimlik (Guid) ile gösterir.
/// </summary>
internal sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.ToTable("orders", SahaNetDbContext.PaymentsSchema);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Amount).HasPrecision(14, 2);
        builder.Property(o => o.Commission).HasPrecision(14, 2);
        builder.Property(o => o.CommissionRate).HasPrecision(6, 4);
        builder.Property(o => o.Currency).HasMaxLength(3).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(o => o.Decision).HasConversion<string>().HasMaxLength(24);
        builder.Property(o => o.ProviderPaymentId).HasMaxLength(128);
        builder.Property(o => o.DisputeReason).HasMaxLength(PaymentOrder.ReasonMaxLength);
        builder.Property(o => o.DecisionNote).HasMaxLength(PaymentOrder.ReasonMaxLength);
        builder.Property<uint>("RowVersion").IsRowVersion();
        builder.Ignore(o => o.SellerPayout);
        builder.Ignore(o => o.DomainEvents);

        builder.HasIndex(o => o.OfferId);
        builder.HasIndex(o => o.BuyerId);
        builder.HasIndex(o => o.SellerId);
        builder.HasIndex(o => new { o.Status, o.PayBy });
        builder.HasIndex(o => new { o.Status, o.AutoReleaseAt });
        builder.HasIndex(o => o.ProviderPaymentId).IsUnique();
    }
}
