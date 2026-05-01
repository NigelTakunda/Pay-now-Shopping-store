using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PayNowStore.Api.Models;

namespace PayNowStore.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var passwordHasher = new PasswordHasher<User>();
        var adminUser = new User
        {
            Id = 1,
            Name = "Store",
            Surname = "Admin",
            FullName = "Store Admin",
            Email = "admin@paynowstore.local",
            Role = "Admin",
            CreatedAtUtc = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc)
        };
        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Admin123!");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .Property(p => p.Price)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Order>()
            .Property(o => o.TotalAmount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<OrderItem>()
            .Property(oi => oi.UnitPrice)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<User>().HasData(adminUser);

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 101, Name = "iPhone 16 Pro Max", Description = "Apple flagship smartphone with premium camera performance and a large display.", Category = "Smartphones", ImageUrl = "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=900&q=80", Price = 1599.99m, InStock = true },
            new Product { Id = 102, Name = "Samsung S25", Description = "Samsung high-end Android phone built for fast performance and vibrant visuals.", Category = "Smartphones", ImageUrl = "https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?auto=format&fit=crop&w=900&q=80", Price = 1399.99m, InStock = true },
            new Product { Id = 103, Name = "Pixel XL", Description = "Google smartphone with clean Android software and advanced AI-powered photography.", Category = "Smartphones", ImageUrl = "https://images.unsplash.com/photo-1598327105666-5b89351aff97?auto=format&fit=crop&w=900&q=80", Price = 1199.99m, InStock = true },
            new Product { Id = 104, Name = "Oppo Reno", Description = "Stylish Oppo device with strong battery life and smooth everyday performance.", Category = "Smartphones", ImageUrl = "https://images.unsplash.com/photo-1580910051074-3eb694886505?auto=format&fit=crop&w=900&q=80", Price = 899.99m, InStock = true },
            new Product { Id = 105, Name = "Huawei P Series", Description = "Huawei smartphone focused on elegant design, camera quality, and premium build.", Category = "Smartphones", ImageUrl = "https://images.unsplash.com/photo-1567581935884-3349723552ca?auto=format&fit=crop&w=900&q=80", Price = 1099.99m, InStock = true }
        );
    }
}
