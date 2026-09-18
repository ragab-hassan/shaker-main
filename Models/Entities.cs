using System.ComponentModel.DataAnnotations;

namespace HousingManagementWeb.Models;

public class AppUser
{
    public int Id { get; set; }
    [Required, Display(Name = "اسم المستخدم")] public string UserName { get; set; } = "";
    [Required, Display(Name = "الاسم الكامل")] public string FullName { get; set; } = "";
    [Required, EmailAddress, Display(Name = "البريد الإلكتروني")] public string Email { get; set; } = "";
    [Required, Display(Name = "كلمة المرور")] public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<UserRole> UserRoles { get; set; } = [];
}

public class AppRole
{
    public int Id { get; set; }
    [Required, Display(Name = "اسم الدور")] public string Name { get; set; } = "";
    [Display(Name = "الوصف")] public string Description { get; set; } = "";
    public bool IsSystemRole { get; set; }
    public List<UserRole> UserRoles { get; set; } = [];
    public List<RolePermission> RolePermissions { get; set; } = [];
}

public class AuditLog
{
    public int Id { get; set; }
    [Display(Name = "اسم المستخدم")] public string UserName { get; set; } = "";
    [Display(Name = "الإجراء")] public string Action { get; set; } = "";
    [Display(Name = "الكيان")] public string EntityName { get; set; } = "";
    [Display(Name = "التفاصيل")] public string Details { get; set; } = "";
    [Display(Name = "تاريخ التشغيل")] public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Permission
{
    public int Id { get; set; }
    [Required, Display(Name = "اسم الصلاحية")] public string Name { get; set; } = "";
    [Display(Name = "الوصف")] public string Description { get; set; } = "";
    public List<RolePermission> RolePermissions { get; set; } = [];
}

public class UserRole
{
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int RoleId { get; set; }
    public AppRole Role { get; set; } = null!;
}

public class RolePermission
{
    public int RoleId { get; set; }
    public AppRole Role { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

public static class AppPermissions
{
    public const string ManageUsers = "ManageUsers";
    public const string ManageRoles = "ManageRoles";
    public const string ViewDashboard = "ViewDashboard";
    public const string ManageEmployees = "ManageEmployees";
    public const string ManageProjects = "ManageProjects";
    public const string ManageLocations = "ManageLocations";
    public const string ManageVehicles = "ManageVehicles";
    public const string ManageHousingAssignments = "ManageHousingAssignments";
}

public class Employee
{
    public int Id { get; set; }
    [Display(Name="كود الموظف")] public string EmployeeCode { get; set; } = "";
    [Required, Display(Name="اسم الموظف")] public string FullName { get; set; } = "";
    [Display(Name="الرقم القومي")] public string NationalId { get; set; } = "";
    [Display(Name="العنوان")] public string Address { get; set; } = "";
    [Display(Name="المحافظة")] public string Governorate { get; set; } = "";
    [Display(Name="الموبايل")] public string Phone { get; set; } = "";
    [Display(Name="المشروع")] public string ProjectName { get; set; } = "";
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    [Display(Name="الوظيفة")] public string JobTitle { get; set; } = "";
    public int? BuildingNo { get; set; }
    public int? ApartmentNo { get; set; }
    public int? RoomNo { get; set; }
    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    [Display(Name="تاريخ التسكين")] public DateTime? HousingDate { get; set; }
    [Display(Name="تاريخ الاستقالة")] public DateTime? ResignationDate { get; set; }
    public bool IsActive { get; set; } = true;
    public List<HousingAssignment> HousingAssignments { get; set; } = [];
}

public class Project
{
    public int Id { get; set; }
    [Required, Display(Name="اسم المشروع")] public string Name { get; set; } = "";
    [Display(Name="الوصف")] public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<Employee> Employees { get; set; } = [];
    public List<Vehicle> Vehicles { get; set; } = [];
}

public class Site
{
    public int Id { get; set; }
    [Required, Display(Name="اسم المكان")] public string Name { get; set; } = "";
    [Display(Name="العنوان")] public string Address { get; set; } = "";
    public List<Building> Buildings { get; set; } = [];
}

public class Building
{
    public int Id { get; set; }
    [Display(Name="رقم العمارة")] public int Number { get; set; }
    [Required] public string Name { get; set; } = "";
    public int SiteId { get; set; }
    public Site? Site { get; set; }
    public List<Apartment> Apartments { get; set; } = [];
}

public class Apartment
{
    public int Id { get; set; }
    public int Number { get; set; }
    public int BuildingId { get; set; }
    public Building? Building { get; set; }
    public List<Room> Rooms { get; set; } = [];
}

public class Room
{
    public int Id { get; set; }
    public int Number { get; set; }
    public bool IsOccupied { get; set; }
    public int ApartmentId { get; set; }
    public Apartment? Apartment { get; set; }
    public List<Employee> Employees { get; set; } = [];
}

public class HousingAssignment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
}

public class PendingHousingAssignment
{
    public int Id { get; set; }
    [Required, Display(Name = "اسم الموظف")] public string EmployeeName { get; set; } = "";
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    [Required, Display(Name = "الوظيفة")] public string JobTitle { get; set; } = "";
    [Required, Display(Name = "المشروع")] public int ProjectId { get; set; }
    public Project? Project { get; set; }
    [Required, Display(Name = "المنطقة")] public int SiteId { get; set; }
    public Site? Site { get; set; }
    [Required, Display(Name = "الغرفة")] public int RoomId { get; set; }
    public Room? Room { get; set; }
    [Required, Display(Name = "تاريخ التسكين")] public DateTime HousingDate { get; set; } = DateTime.Today;
    public bool IsApplied { get; set; }
}

public class Vehicle
{
    public int Id { get; set; }
    [Required, Display(Name="كود السيارة")] public string VehicleCode { get; set; } = "";
    [Required, Display(Name="رقم السيارة")] public string VehicleNumber { get; set; } = "";
    [Display(Name="رقم اللوحة")] public string PlateNumber { get; set; } = "";
    [Required, Display(Name="نوع العربية")] public string VehicleType { get; set; } = "";
    [Display(Name="السائق")] public string? DriverName { get; set; }
    public int? DriverEmployeeId { get; set; }
    public Employee? DriverEmployee { get; set; }
    [Display(Name="المشروع")] public string? ProjectName { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    [Display(Name="السعة")] public int Capacity { get; set; }
    [Display(Name="خط السير")] public string? Route { get; set; }
    [Display(Name="ملاحظات")] public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<VehiclePassenger> Passengers { get; set; } = [];
}

public class VehiclePassenger
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
}
