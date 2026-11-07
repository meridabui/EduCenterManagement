using EduCenterManagement.ViewModels.Teacher.Attendance;

namespace EduCenterManagement.Services.Teacher
{
    public interface ITeacherAttendanceService
    {
        Task<ClassSessionPickerViewModel?> GetPickerViewModelAsync(int userId, int? classId);

        Task<AttendanceSheetViewModel?> GetAttendanceSheetAsync(int userId, int sessionId);

        Task<(bool Success, string Message)> SaveAttendanceSheetAsync(int userId, AttendanceSheetViewModel model);

        Task<List<object>> GetSessionsByClassAsync(int userId, int classId);
    }
}

