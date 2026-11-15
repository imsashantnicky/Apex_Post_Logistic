using ApexPost.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Tracking.Configurations;

/// <summary>
/// The TrackingScans table: append-only history. The unique ScanId index is what makes a second upload of the same
/// scan a duplicate. Scans never disappear with their shipment (Restrict).
/// </summary>
internal sealed class TrackingScanConfiguration : IEntityTypeConfiguration<TrackingScan>
{
    public const string TableName = "TrackingScans";
    public const string ScanIdIndex = "IX_TrackingScans_ScanId";
    public const string ShipmentTimeIndex = "IX_TrackingScans_ShipmentId_ScannedAtUtc";

    public void Configure(EntityTypeBuilder<TrackingScan> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint("CK_TrackingScans_ScanType", TrackingColumns.In<ScanType>("ScanType"));
            table.HasCheckConstraint("CK_TrackingScans_ScanId", "[ScanId] LIKE 'scan::%'");
            table.HasCheckConstraint("CK_TrackingScans_CourierId", "[CourierId] LIKE 'CR-[0-9][0-9][0-9]'");
        });
        builder.HasKey(scan => scan.Id);
        builder.Property(scan => scan.Id).ValueGeneratedNever();
        builder.Ignore(scan => scan.Version);

        builder.Property(scan => scan.ScanId).HasMaxLength(TrackingFormats.ScanIdLength).IsUnicode(false);
        builder.HasIndex(scan => scan.ScanId).IsUnique().HasDatabaseName(ScanIdIndex);

        builder.Property(scan => scan.ScanType).AsEnumText();
        builder.Property(scan => scan.CourierId).HasMaxLength(TrackingFormats.CourierIdLength).IsUnicode(false);
        builder.Property(scan => scan.Location).HasMaxLength(TrackingScan.LocationLength);
        builder.Property(scan => scan.Notes).HasMaxLength(TrackingScan.NotesLength);
        builder.Property(scan => scan.ScannedAtUtc);
        builder.Property(scan => scan.ReceivedAtUtc);

        builder.HasOne<TrackedShipment>().WithMany().HasForeignKey(scan => scan.ShipmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(scan => new { scan.ShipmentId, scan.ScannedAtUtc }).HasDatabaseName(ShipmentTimeIndex);
    }
}
