using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using KennelManagementSystemAPI.Models;

namespace KennelManagementSystemAPI.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Dog> Dogs { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Kennel> Kennels { get; set; }
    public DbSet<Booking> Bookings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure relationships
        builder.Entity<Dog>()
            .HasOne(d => d.Customer)
            .WithMany(c => c.Dogs)
            .HasForeignKey(d => d.CustomerId);

        builder.Entity<Booking>()
            .HasOne(b => b.Dog)
            .WithMany(d => d.Bookings)
            .HasForeignKey(b => b.DogId);

        builder.Entity<Booking>()
            .HasOne(b => b.Kennel)
            .WithMany(k => k.Bookings)
            .HasForeignKey(b => b.KennelId);
    }
}
