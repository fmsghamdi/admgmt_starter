using Microsoft.EntityFrameworkCore;
using admgmt_backend.Models;

namespace admgmt_backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<AppUserNote> AppUserNotes => Set<AppUserNote>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<AuditLog>().HasKey(x => x.Id);
            modelBuilder.Entity<AppUserNote>().HasKey(x => x.Id);
        }
    }
}
