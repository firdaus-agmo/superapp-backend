using Microsoft.EntityFrameworkCore;

namespace SuperAppBackend;

// 1. The User Table Layout
public class AppUser
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; 
}

// 2. The Database Connection
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users { get; set; }
}