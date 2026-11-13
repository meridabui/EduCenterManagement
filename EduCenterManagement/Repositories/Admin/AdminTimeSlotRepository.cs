using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Admin;

public class AdminTimeSlotRepository : IAdminTimeSlotRepository
{
    private readonly ApplicationDbContext _context;
    public AdminTimeSlotRepository(ApplicationDbContext context) => _context = context;
    public Task<List<TimeSlot>> GetAllAsync(bool orderByTime = true, bool tracking = false)
    {
        var query = tracking ? _context.TimeSlots : _context.TimeSlots.AsNoTracking();
        return (orderByTime ? query.OrderBy(t => t.StartTime) : query).ToListAsync();
    }
    public Task<TimeSlot?> GetByIdAsync(int id, bool tracking = false)
        => (tracking ? _context.TimeSlots : _context.TimeSlots.AsNoTracking()).FirstOrDefaultAsync(t => t.Id == id);
    public Task<int> LockAsync(int id) => _context.TimeSlots.Where(t => t.Id == id)
        .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, t => t.UpdatedAt));
    public Task<bool> NameExistsAsync(string name, int? exceptId = null) => _context.TimeSlots
        .AnyAsync(t => t.Name == name && (!exceptId.HasValue || t.Id != exceptId.Value));
    public Task<bool> OverlapsAsync(TimeOnly start, TimeOnly end, int? exceptId = null, bool activeOnly = false)
        => _context.TimeSlots.AnyAsync(t => (!exceptId.HasValue || t.Id != exceptId.Value)
            && (!activeOnly || t.Status == TimeSlotStatus.Active) && start < t.EndTime && end > t.StartTime);
    public Task<List<TimeSlotClassUsageViewModel>> GetUsageAsync(int id) => _context.ClassSchedules.AsNoTracking()
        .Where(s => s.TimeSlotId == id).Select(s => new { s.ClassId, s.Class.Name }).Distinct().OrderBy(c => c.Name)
        .Select(c => new TimeSlotClassUsageViewModel { Id = c.ClassId, Name = c.Name }).ToListAsync();
    public void Add(TimeSlot slot) => _context.TimeSlots.Add(slot);
    public void AddRange(IEnumerable<TimeSlot> slots) => _context.TimeSlots.AddRange(slots);
}
