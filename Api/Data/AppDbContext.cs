using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<ResourceConstraint> ResourceConstraints => Set<ResourceConstraint>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Name).HasMaxLength(120);
            e.Property(u => u.Email).HasMaxLength(200);
        });

        modelBuilder.Entity<Resource>(e =>
        {
            e.Property(r => r.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Name).HasMaxLength(120);
            e.Property(r => r.Location).HasMaxLength(200);
            e.Property(r => r.Description).HasMaxLength(1000);
            e.HasMany(r => r.Constraints).WithOne().HasForeignKey(c => c.ResourceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResourceConstraint>(e =>
        {
            e.Property(c => c.Type).HasConversion<string>().HasMaxLength(40);
            e.Property(c => c.ParamsJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.Property(b => b.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(b => b.Notes).HasMaxLength(500);
            e.HasOne(b => b.Resource).WithMany(r => r.Bookings).HasForeignKey(b => b.ResourceId);
            e.HasOne(b => b.User).WithMany(u => u.Bookings).HasForeignKey(b => b.UserId);
            e.HasIndex(b => new { b.ResourceId, b.Start });
            e.HasIndex(b => new { b.UserId, b.Start });
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.Property(n => n.Type).HasMaxLength(40);
            e.Property(n => n.Message).HasMaxLength(500);
            e.HasIndex(n => new { n.UserId, n.ReadAt });
        });
    }
}
