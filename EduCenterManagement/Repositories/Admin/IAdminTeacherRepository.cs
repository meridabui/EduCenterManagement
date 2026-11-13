using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Admin;

public interface IAdminTeacherRepository
{
    Task<List<Subject>> GetFilterSubjectsAsync();
    Task<List<TeacherProfile>> GetTeachersAsync(int? subjectId);
    Task<bool> EmailExistsAsync(string email);
    Task<TeacherProfile?> GetByIdAsync(int id, bool tracking = false);
    Task<List<Subject>> GetFormSubjectsAsync(List<int> currentSubjectIds);
    Task<int> CountValidSubjectsAsync(List<int> subjectIds, List<int> currentSubjectIds);
    Task<List<string>> GetBlockingClassesAsync(int teacherId, List<int> removedSubjectIds);
    void AddUser(AppUser user);
    void AddTeacher(TeacherProfile teacher);
    void RemoveSubjects(IEnumerable<TeacherSubject> subjects);
}
