using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Admin;

public interface IAdminSubjectRepository
{
    Task<int> CountAsync();
    Task<List<Subject>> GetPageAsync(int offset, int pageSize);
    Task<Subject?> GetByIdAsync(int id, bool tracking = false);
    Task<bool> NameExistsAsync(string name, int? exceptId = null);
    void Add(Subject subject);
}
