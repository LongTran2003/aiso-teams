using AISO.Domain.Entities;
using AISO.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISO.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserMapping> UserMappings => Set<UserMapping>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // UserMapping
        modelBuilder.Entity<UserMapping>(b =>
        {
            b.ToTable("user_mappings");
            b.HasKey(x => x.Id);
            b.Property(x => x.TeamsUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.TeamsEmail).HasMaxLength(256);
            b.Property(x => x.SapUsername).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.TeamsUserId).IsUnique();
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("audit_logs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).UseIdentityAlwaysColumn();
            b.Property(x => x.TeamsUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.ConversationId).HasMaxLength(256);
            b.Property(x => x.Action).HasMaxLength(64).IsRequired();
            b.Property(x => x.ParametersJson).HasColumnType("jsonb");
            b.Property(x => x.ResultStatus).HasMaxLength(32).IsRequired();
            b.Property(x => x.ErrorMessage).HasMaxLength(2000);
            b.HasIndex(x => x.Timestamp);
            b.HasIndex(x => new { x.TeamsUserId, x.Timestamp });
        });

        base.OnModelCreating(modelBuilder);
    }
}
