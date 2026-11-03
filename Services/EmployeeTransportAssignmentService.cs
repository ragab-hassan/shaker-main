using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Services;

public sealed class EmployeeTransportAssignmentService
{
    private readonly AppDbContext _db;

    public EmployeeTransportAssignmentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> ReserveSeatForHousingAsync(Employee employee)
    {
        var existingReservations = await _db.VehiclePassengers
            .Where(passenger => passenger.EmployeeId == employee.Id)
            .ToListAsync();

        if (!employee.IsActive || employee.ResignationDate.HasValue || !employee.RoomId.HasValue)
        {
            await RemoveReservationsAsync(existingReservations);
            return null;
        }

        var siteName = await _db.Rooms
            .Where(room => room.Id == employee.RoomId.Value)
            .Select(room => room.Apartment!.Building!.Site!.Name)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(siteName))
        {
            await RemoveReservationsAsync(existingReservations);
            return null;
        }

        var normalizedSiteName = NormalizeSiteName(siteName);
        var vehicleType = normalizedSiteName.Contains("مديرين", StringComparison.Ordinal)
            ? "ملاكي"
            : normalizedSiteName.Contains("مطروح", StringComparison.Ordinal)
                || normalizedSiteName.Contains("سيديعبدالرحمن", StringComparison.Ordinal)
                    ? "هاي اس"
                    : null;
        if (vehicleType == null)
        {
            await RemoveReservationsAsync(existingReservations);
            return null;
        }

        if (!employee.ProjectId.HasValue)
        {
            await RemoveReservationsAsync(existingReservations);
            return $"لم يتم حجز مقعد للموظف {employee.FullName}: يجب ربطه بمشروع أولًا.";
        }

        var vehicles = await _db.Vehicles
            .Where(vehicle => vehicle.IsActive
                && vehicle.ProjectId == employee.ProjectId
                && vehicle.VehicleType == vehicleType)
            .Include(vehicle => vehicle.Passengers)
            .ToListAsync();
        if (vehicles.Count == 0)
        {
            await RemoveReservationsAsync(existingReservations);
            return $"لم يتم حجز مقعد للموظف {employee.FullName}: لا توجد سيارة نشطة من نوع {vehicleType} للمشروع المحدد.";
        }

        var vehicle = vehicles
            .Where(candidate => HasAvailableSeat(candidate, employee.Id))
            .OrderByDescending(candidate =>
                candidate.DriverEmployeeId == employee.Id
                || existingReservations.Any(passenger => passenger.VehicleId == candidate.Id))
            .ThenByDescending(GetOccupiedSeatCount)
            .ThenBy(candidate => candidate.Id)
            .FirstOrDefault();
        if (vehicle == null)
        {
            await RemoveReservationsAsync(existingReservations);
            return $"لم يتم حجز مقعد للموظف {employee.FullName}: سيارات {vehicleType} التابعة للمشروع المحدد ممتلئة.";
        }

        if (vehicle.DriverEmployeeId == employee.Id)
        {
            _db.VehiclePassengers.RemoveRange(existingReservations);
            await _db.SaveChangesAsync();
            return null;
        }

        if (!existingReservations.Any(passenger => passenger.VehicleId == vehicle.Id))
        {
            _db.VehiclePassengers.Add(new VehiclePassenger
            {
                VehicleId = vehicle.Id,
                EmployeeId = employee.Id
            });
        }

        _db.VehiclePassengers.RemoveRange(
            existingReservations.Where(passenger => passenger.VehicleId != vehicle.Id));
        await _db.SaveChangesAsync();
        return null;
    }

    private async Task RemoveReservationsAsync(List<VehiclePassenger> reservations)
    {
        if (reservations.Count == 0) return;

        _db.VehiclePassengers.RemoveRange(reservations);
        await _db.SaveChangesAsync();
    }

    private static bool HasAvailableSeat(Vehicle vehicle, int employeeId)
    {
        var passengerIds = vehicle.Passengers
            .Select(passenger => passenger.EmployeeId)
            .ToHashSet();
        if (vehicle.DriverEmployeeId.HasValue)
        {
            passengerIds.Add(vehicle.DriverEmployeeId.Value);
        }

        return passengerIds.Contains(employeeId) || passengerIds.Count < vehicle.Capacity;
    }

    private static int GetOccupiedSeatCount(Vehicle vehicle)
    {
        var passengerIds = vehicle.Passengers
            .Select(passenger => passenger.EmployeeId)
            .ToHashSet();
        if (vehicle.DriverEmployeeId.HasValue)
        {
            passengerIds.Add(vehicle.DriverEmployeeId.Value);
        }

        return passengerIds.Count;
    }

    private static string NormalizeSiteName(string siteName)
    {
        return string.Concat(siteName
                .Where(character => !char.IsWhiteSpace(character)
                    && character != 'ـ'
                    && character is not (>= '\u064B' and <= '\u065F')
                    && character != '\u0670'))
            .Replace('ى', 'ي');
    }
}
