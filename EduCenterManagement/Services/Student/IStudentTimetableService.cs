using EduCenterManagement.ViewModels.Student;

namespace EduCenterManagement.Services.Student
{
    public interface IStudentTimetableService
    {
        Task<WeeklyTimetableViewModel?> GetWeeklyTimetableAsync(int userId, int weekOffset);

        Task<ClassDetailViewModel?> GetClassDetailAsync(int userId, int classId);

        Task<(byte[]? FileContent, string? PhysicalPath, string ContentType, string DownloadFileName)> GetSlideDownloadAsync(int userId, int slideId, string webRootPath);
    }
}
