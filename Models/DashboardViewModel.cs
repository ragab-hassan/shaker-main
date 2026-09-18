namespace HousingManagementWeb.Models;

public class DashboardViewModel
{
    public int TotalEmployees { get; set; }
    public int TotalProjects { get; set; }
    public int TotalVehicles { get; set; }
    public int TotalPassengers { get; set; }
    public int TotalRooms { get; set; }
    public int OccupiedRooms { get; set; }
    public int EmptyRooms => TotalRooms - OccupiedRooms;
    public int TotalSites { get; set; }
    public int TotalBuildings { get; set; }
    public List<ProjectDashboardRow> Projects { get; set; } = [];
    public List<SiteDashboardRow> Sites { get; set; } = [];
    public List<BuildingDashboardRow> Buildings { get; set; } = [];
}

public class ProjectDashboardRow
{
    public string Name { get; set; } = "";
    public int Employees { get; set; }
    public int Vehicles { get; set; }
    public int Passengers { get; set; }
}

public class SiteDashboardRow
{
    public string Name { get; set; } = "";
    public int Rooms { get; set; }
    public int Occupied { get; set; }
    public int Empty => Rooms - Occupied;
}

public class BuildingDashboardRow
{
    public string SiteName { get; set; } = "";
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public int Rooms { get; set; }
    public int Occupied { get; set; }
    public int Empty => Rooms - Occupied;
}
