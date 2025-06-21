using Microsoft.EntityFrameworkCore;
using aspnet_core_api.Models;

namespace aspnet_core_api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
    }
}