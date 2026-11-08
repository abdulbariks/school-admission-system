using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Data;

public sealed class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<AdmissionHold> Holds => Set<AdmissionHold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolClass>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PublicId).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.ToTable(t => t.HasCheckConstraint("CK_Classes_TotalSeats", "\"TotalSeats\" > 0"));
        });
        modelBuilder.Entity<AdmissionHold>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PublicId).IsUnique();
            e.HasIndex(x => new { x.ClassId, x.StudentId, x.Status });
            e.HasOne(x => x.Class).WithMany(x => x.Holds).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.StudentId).HasMaxLength(17).IsRequired();
            e.Property(x => x.Status).HasMaxLength(12).IsRequired();
            e.ToTable(t => t.HasCheckConstraint("CK_Holds_Status", "\"Status\" IN ('HELD','CONFIRMED','EXPIRED','CANCELLED')"));
        });
    }
}
