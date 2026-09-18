using HousingManagementWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppRole> AppRoles => Set<AppRole>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Apartment> Apartments => Set<Apartment>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<HousingAssignment> HousingAssignments => Set<HousingAssignment>();
    public DbSet<PendingHousingAssignment> PendingHousingAssignments => Set<PendingHousingAssignment>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehiclePassenger> VehiclePassengers => Set<VehiclePassenger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(u => u.UserName).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<AppRole>().HasIndex(r => r.Name).IsUnique();
        modelBuilder.Entity<Permission>().HasIndex(p => p.Name).IsUnique();

        modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
        modelBuilder.Entity<UserRole>().HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserRole>().HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.PermissionId });
        modelBuilder.Entity<RolePermission>().HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RolePermission>().HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Employee>().HasOne(e => e.Project).WithMany(p => p.Employees).HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Employee>().HasOne(e => e.Room).WithMany(r => r.Employees).HasForeignKey(e => e.RoomId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Vehicle>().HasOne(v => v.Project).WithMany(p => p.Vehicles).HasForeignKey(v => v.ProjectId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Vehicle>().HasOne(v => v.DriverEmployee).WithMany().HasForeignKey(v => v.DriverEmployeeId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Building>().HasOne(b => b.Site).WithMany(s => s.Buildings).HasForeignKey(b => b.SiteId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Apartment>().HasOne(a => a.Building).WithMany(b => b.Apartments).HasForeignKey(a => a.BuildingId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Room>().HasOne(r => r.Apartment).WithMany(a => a.Rooms).HasForeignKey(r => r.ApartmentId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HousingAssignment>().HasOne(h => h.Employee).WithMany(e => e.HousingAssignments).HasForeignKey(h => h.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HousingAssignment>().HasOne(h => h.Room).WithMany().HasForeignKey(h => h.RoomId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PendingHousingAssignment>().HasOne(p => p.Employee).WithMany().HasForeignKey(p => p.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PendingHousingAssignment>().HasOne(p => p.Project).WithMany().HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PendingHousingAssignment>().HasOne(p => p.Site).WithMany().HasForeignKey(p => p.SiteId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PendingHousingAssignment>().HasOne(p => p.Room).WithMany().HasForeignKey(p => p.RoomId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VehiclePassenger>().HasOne(vp => vp.Vehicle).WithMany(v => v.Passengers).HasForeignKey(vp => vp.VehicleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VehiclePassenger>().HasOne(vp => vp.Employee).WithMany().HasForeignKey(vp => vp.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VehiclePassenger>().HasIndex(vp => new { vp.VehicleId, vp.EmployeeId }).IsUnique();
    }
}
