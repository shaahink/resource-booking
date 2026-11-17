using Api.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<ResourceConstraint> ResourceConstraints => Set<ResourceConstraint>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>The key ring that signs auth cookies — in the database so sessions survive a restart.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(FieldLimits.EnumName);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Name).HasMaxLength(FieldLimits.Name);
            e.Property(u => u.Email).HasMaxLength(FieldLimits.Email);
        });

        modelBuilder.Entity<Resource>(e =>
        {
            e.Property(r => r.Type).HasConversion<string>().HasMaxLength(FieldLimits.EnumName);
            e.Property(r => r.Name).HasMaxLength(FieldLimits.Name);
            e.Property(r => r.Slug).HasMaxLength(FieldLimits.Slug);
            e.HasIndex(r => r.Slug).IsUnique();
            e.Property(r => r.Location).HasMaxLength(FieldLimits.Location);
            e.Property(r => r.Description).HasMaxLength(FieldLimits.Description);
            e.HasMany(r => r.Constraints).WithOne().HasForeignKey(c => c.ResourceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResourceConstraint>(e =>
        {
            e.Property(c => c.Type).HasConversion<string>().HasMaxLength(FieldLimits.TypeName);
            e.Property(c => c.ParamsJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.Property(b => b.Status).HasConversion<string>().HasMaxLength(FieldLimits.EnumName);
            e.Property(b => b.Notes).HasMaxLength(FieldLimits.Notes);
            e.HasOne(b => b.Resource).WithMany(r => r.Bookings).HasForeignKey(b => b.ResourceId);
            e.HasOne(b => b.User).WithMany(u => u.Bookings).HasForeignKey(b => b.UserId);
            e.HasIndex(b => new { b.ResourceId, b.Start });
            e.HasIndex(b => new { b.UserId, b.Start });
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.Property(n => n.Type).HasMaxLength(FieldLimits.TypeName);
            e.Property(n => n.Message).HasMaxLength(FieldLimits.NotificationMessage);
            e.HasIndex(n => new { n.UserId, n.ReadAt });
        });

        // Pre-existing rows (added before these columns) read as created "now" — the honest default.
        modelBuilder.Entity<User>().Property(u => u.CreatedOn).HasDefaultValueSql("now()");
        modelBuilder.Entity<Resource>().Property(r => r.CreatedOn).HasDefaultValueSql("now()");
    }
}
