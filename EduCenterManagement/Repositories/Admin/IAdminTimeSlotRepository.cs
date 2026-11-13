using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Admin;

namespace EduCenterManagement.Repositories.Admin;

public interface IAdminTimeSlotRepository
{
    Task<List<TimeSlot>> GetAllAsync(bool orderByTime = true, bool tracking = false);
    Task<TimeSlot?> GetByIdAsync(int id, bool tracking = false);
    Task<int> LockAsync(int id);
    Task<bool> NameExistsAsync(string name, int? exceptId = null);
    Task<bool> OverlapsAsync(TimeOnly start, TimeOnly end, int? exceptId = null, bool activeOnly = false);
    Task<List<TimeSlotClassUsageViewModel>> GetUsageAsync(int id);
    void Add(TimeSlot slot);
    void AddRange(IEnumerable<TimeSlot> slots);
}
