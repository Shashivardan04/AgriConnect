using AgriConnect.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AgriConnect.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ListingReport> ListingReports => Set<ListingReport>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Listing>(e =>
        {
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Property(x => x.Quantity).HasPrecision(10, 2);
            e.Ignore(x => x.IsBookable);
            e.Ignore(x => x.BookingResourceKey);

            // The search path: filter by district, then type, then exclude removed.
            e.HasIndex(x => new { x.District, x.Type, x.Status });

            // A worker registers once per task. Filtered to labour, because owning two
            // tractors in the same category is perfectly normal.
            //
            // Note: a filtered index makes SQL Server require QUOTED_IDENTIFIER ON for
            // ANY insert, update or delete on this table. EF Core always sets it, so
            // the application is unaffected, but ad-hoc sqlcmd defaults it OFF and such
            // statements fail with error 1934 until the script sets it explicitly.
            e.HasIndex(x => new { x.OwnerId, x.CategoryId })
                .IsUnique()
                .HasFilter($"[Type] = {(int)ListingType.Labour}");

            // Finding every task a given worker offers.
            e.HasIndex(x => new { x.Type, x.OwnerId, x.Status });

            // Restrict, not Cascade: Listing and Booking both point at AspNetUsers,
            // and SQL Server refuses multiple cascade paths to the same table.
            e.HasOne(x => x.Owner).WithMany()
                .HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany()
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Booking>(e =>
        {
            e.Ignore(x => x.IsOwnerBlock);

            // Covers the overlap check in BookingService.
            e.HasIndex(x => new { x.ListingId, x.FromDate });

            e.HasOne(x => x.Listing).WithMany(l => l.Bookings)
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Requester).WithMany()
                .HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ListingImage>()
            .HasOne(x => x.Listing).WithMany(l => l.Images)
            .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<MarketPrice>(e =>
        {
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.HasIndex(x => new { x.CategoryId, x.District });
            e.HasOne(x => x.Category).WithMany()
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Notification>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ListingReport>(e =>
        {
            e.HasOne(x => x.Listing).WithMany()
                .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ReportedBy).WithMany()
                .HasForeignKey(x => x.ReportedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Category>().HasIndex(x => new { x.ListingType, x.Name }).IsUnique();
    }
}
