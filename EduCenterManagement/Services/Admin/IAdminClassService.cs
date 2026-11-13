using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Admin;

namespace EduCenterManagement.Services.Admin;

public interface IAdminClassService
{
    Task<AdminResult<AdminClassIndexData>> GetIndexAsync(int? subjectId, int? teacherId, string? status, int page);
    Task<AdminResult> CreateAsync(ClassViewModel model);
    Task<AdminResult<object>> CheckConflictAsync(int teacherId, int? classId, DateOnly? startDate, DateOnly? endDate,
        int? timeSlotId, int[] days, int[] timeSlotIds);
    Task<AdminResult<object>> GetAssignInfoAsync(int id);
    Task<AdminResult<object>> AssignTeacherAsync(AssignTeacherViewModel model);
    Task<AdminResult<object>> GetCapacityAsync(int id);
    Task<AdminClassDetailsData?> GetDetailsAsync(int id);
    Task<AdminResult> GenerateSessionsAsync(int id);
    Task<AdminResult<AdminClassEditData>> GetEditAsync(int id);
    Task<AdminResult<AdminClassEditData>> EditAsync(int id, ClassViewModel model, bool inputValid);
    Task<AdminResult<Class>> GetDeleteAsync(int id);
    Task<ClassTransitionResult> ChangeStatusAsync(int id, ClassStatus targetStatus);
    Task<object> GetTeachersBySubjectAsync(int subjectId);
}
