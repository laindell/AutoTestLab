using Microsoft.EntityFrameworkCore;
using server.Models;

namespace server.Data;

public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<UsersFiles> UsersFiles => Set<UsersFiles>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>(); 
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<TestGroupShare> TestGroupShares { get; set; }
    public DbSet<GradeForTheTest> GradesForTheTests => Set<GradeForTheTest>();
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<GroupMember>()
            .HasKey(gm => new { gm.GroupId, gm.UserId });

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GroupId);

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.User)
            .WithMany(u => u.GroupMemberships)
            .HasForeignKey(gm => gm.UserId);

        modelBuilder.Entity<TestGroupShare>()
            .HasKey(tgs => new { tgs.TestId, tgs.GroupId });

        modelBuilder.Entity<TestGroupShare>()
            .HasOne(tgs => tgs.Test)
            .WithMany(t => t.SharedGroups) 
            .HasForeignKey(tgs => tgs.TestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestGroupShare>()
            .HasOne(tgs => tgs.Group)
            .WithMany() 
            .HasForeignKey(tgs => tgs.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}