using AISO.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISO.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserMapping> UserMappings => Set<UserMapping>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OrderApproval> OrderApprovals => Set<OrderApproval>();
    public DbSet<SapLinkAssignment> SapLinkAssignments => Set<SapLinkAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // UserMapping
        modelBuilder.Entity<UserMapping>(b =>
        {
            b.ToTable("user_mappings");
            b.HasKey(x => x.Id);
            b.Property(x => x.TeamsUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
            b.Property(x => x.SapUserId).HasMaxLength(64);
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.SalesOrg).HasMaxLength(8);
            b.HasIndex(x => x.TeamsUserId).IsUnique();
            // SapUserId uniqueness is enforced in link dialog (allow-list + app check).
            // DB unique index is skipped: demo DB may already contain duplicate links.
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

        // OrderApproval (maker-checker)
        modelBuilder.Entity<OrderApproval>(b =>
        {
            b.ToTable("order_approvals");
            b.HasKey(x => x.Id);
            b.Property(x => x.SoNumber).HasMaxLength(20).IsRequired();
            b.Property(x => x.RequestedBySapUser).HasMaxLength(64).IsRequired();
            b.Property(x => x.SalesOrg).HasMaxLength(8);
            b.Property(x => x.Comment).HasMaxLength(500);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.DecidedBySapUser).HasMaxLength(64);
            b.Property(x => x.DecisionComment).HasMaxLength(500);
            b.HasIndex(x => new { x.SoNumber, x.Status });
            b.HasIndex(x => new { x.Status, x.SalesOrg });
        });

        modelBuilder.Entity<SapLinkAssignment>(b =>
        {
            b.ToTable("sap_link_assignments");
            b.HasKey(x => x.Id);
            b.Property(x => x.SapUserId).HasMaxLength(64).IsRequired();
            b.Property(x => x.TeamsEmail).HasMaxLength(256);
            b.Property(x => x.TeamsUserId).HasMaxLength(128);
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.SalesOrg).HasMaxLength(8);
            b.HasIndex(x => x.SapUserId).IsUnique();
            b.HasIndex(x => x.TeamsEmail)
                .IsUnique()
                .HasFilter("\"TeamsEmail\" IS NOT NULL");
            b.HasIndex(x => x.TeamsUserId)
                .IsUnique()
                .HasFilter("\"TeamsUserId\" IS NOT NULL");
        });

        base.OnModelCreating(modelBuilder);
    }
}
