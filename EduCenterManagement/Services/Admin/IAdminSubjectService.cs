using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Admin;

namespace EduCenterManagement.Services.Admin;

public interface IAdminSubjectService
{
    Task<(List<Subject> Subjects, PaginationViewModel Pagination)> GetIndexAsync(int page);
    Task<Subject?> GetByIdAsync(int id);
    Task<SubjectViewModel?> GetEditAsync(int id);
    Task<AdminResult> CreateAsync(SubjectViewModel model);
    Task<AdminResult> EditAsync(int id, SubjectViewModel model);
    Task<AdminResult> DeactivateAsync(int id);
}
