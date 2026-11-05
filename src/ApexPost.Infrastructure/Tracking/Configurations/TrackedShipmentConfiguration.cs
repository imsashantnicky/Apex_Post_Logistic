using System.Globalization;
using ApexPost.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Tracking.Configurations;

/// <summary>
/// The TrackedShipments table. The tracking number is unique, the recipient is a complex type stored in four
/// columns, and Version is the concurrency token the unit of work raises on every change.
/// </summary>
internal sealed class TrackedShipmentConfiguration : IEntityTypeConfiguration<TrackedShipment>
{
    public const string TableName = "TrackedShipments";
    public const string TrackingNumberIndex = "IX_TrackedShipments_TrackingNumber";
    public const string StatusIndex = "IX_TrackedShipments_Status";

    private const string TrackingNumberShape = "[TrackingNumber] LIKE 'APX-[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'";

    public void Configure(EntityTypeBuilder<TrackedShipment> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint("CK_TrackedShipments_Status", TrackingColumns.In<ShipmentStatus>("Status"));
            table.HasCheckConstraint("CK_TrackedShipments_TrackingNumber", TrackingNumberShape);
            table.HasCheckConstraint(
                "CK_TrackedShipments_WeightKg",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"[WeightKg] > 0 AND [WeightKg] <= {TrackedShipment.MaximumWeightKg}"));
        });
        builder.HasKey(shipment => shipment.Id);
        builder.Property(shipment => shipment.Id).ValueGeneratedNever();
        builder.Ignore(shipment => shipment.IsFinal);

        builder.Property(shipment => shipment.TrackingNumber)
            .HasMaxLength(TrackingFormats.TrackingNumberLength).IsUnicode(false);
        builder.HasIndex(shipment => shipment.TrackingNumber).IsUnique().HasDatabaseName(TrackingNumberIndex);

        builder.ComplexProperty(shipment => shipment.Recipient, recipient =>
        {
            recipient.Property(value => value.Name).HasColumnName("RecipientName").HasMaxLength(Recipient.NameLength);
            recipient.Property(value => value.Phone).HasColumnName("RecipientPhone")
                .HasMaxLength(Recipient.PhoneLength).IsUnicode(false);
            recipient.Property(value => value.Address).HasColumnName("RecipientAddress")
                .HasMaxLength(Recipient.AddressLength);
            recipient.Property(value => value.City).HasColumnName("RecipientCity").HasMaxLength(Recipient.CityLength);
        });

        builder.Property(shipment => shipment.WeightKg).HasColumnType(TrackingColumns.Weight);
        builder.Property(shipment => shipment.Status).AsEnumText();
        builder.HasIndex(shipment => shipment.Status).HasDatabaseName(StatusIndex);
        builder.Property(shipment => shipment.CreatedAtUtc);
        builder.Property(shipment => shipment.Version).IsConcurrencyToken();
    }
}
