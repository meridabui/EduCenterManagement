using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Admin;

namespace EduCenterManagement.Services.Admin;

public interface IAdminTimeSlotService
{
    Task<List<TimeSlot>> GetAllAsync();
    Task<TimeSlot?> GetByIdAsync(int id);
    Task<TimeSlotViewModel?> GetEditAsync(int id);
    Task<AdminResult> CreateAsync(TimeSlotViewModel model, bool inputValid);
    Task<AdminResult> EditAsync(int id, TimeSlotViewModel model, bool inputValid, bool inputValidWithoutTimes);
    Task<AdminResult> DeactivateAsync(int id);
    Task<AdminResult> ReactivateAsync(int id);
    Task<AdminResult> GenerateDefaultSlotsAsync();
}
