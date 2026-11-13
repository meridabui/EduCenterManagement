using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.Services.Admin;

public interface IAdminTeacherService
{
    Task<AdminTeacherIndexData> GetIndexAsync(int page, int? subjectId);
    Task<List<SelectListItem>> GetSubjectsAsync(List<int>? selectedSubjectIds = null, List<int>? currentSubjectIds = null);
    Task<AdminResult> CreateAsync(CreateTeacherViewModel model);
    Task<AdminTeacherSubjectsData?> GetAssignSubjectsAsync(int id);
    Task<AdminResult<AdminTeacherSubjectsData>> AssignSubjectsAsync(AssignTeacherSubjectsViewModel model, bool inputValid);
}
