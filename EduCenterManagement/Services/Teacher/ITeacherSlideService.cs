using EduCenterManagement.ViewModels.Teacher.Slide;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.Services.Teacher
{
    public interface ITeacherSlideService
    {
        Task<ClassSessionSlidePickerViewModel> GetClassSessionPickerDataAsync(int userId, int? selectedClassId, int? selectedSessionId);
        Task<List<SelectListItem>> GetSessionsDropdownByClassIdAsync(int userId, int classId);
        Task<SessionSlideListViewModel?> GetSessionSlidesAsync(int userId, int sessionId);
        Task<(bool Success, string Message)> UploadSlideAsync(int userId, UploadSlideInputModel model);
        Task<(bool Success, string Message)> DeleteSlideAsync(int userId, int slideId);
        Task<(bool Success, string? PhysicalPath, string? OriginalFileName, string? MimeType)> GetSlideFileForDownloadAsync(int userId, int slideId);
    }
}

