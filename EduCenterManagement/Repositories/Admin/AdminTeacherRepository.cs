using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Admin;

public class AdminTeacherRepository : IAdminTeacherRepository
{
    private readonly ApplicationDbContext _context;
    public AdminTeacherRepository(ApplicationDbContext context) => _context = context;
    public Task<List<Subject>> GetFilterSubjectsAsync()
        => _context.Subjects.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
    public Task<List<TeacherProfile>> GetTeachersAsync(int? subjectId)
    {
        var query = _context.TeacherProfiles.AsNoTracking();
        if (subjectId.HasValue) query = query.Where(t => t.TeacherSubjects.Any(s => s.SubjectId == subjectId.Value));
        return query.Include(t => t.User).Include(t => t.TeacherSubjects).ThenInclude(ts => ts.Subject).ToListAsync();
    }
    public Task<bool> EmailExistsAsync(string email) => _context.Users.AnyAsync(u => u.Email == email);
    public Task<TeacherProfile?> GetByIdAsync(int id, bool tracking = false)
        => (tracking ? _context.TeacherProfiles : _context.TeacherProfiles.AsNoTracking())
            .Include(t => t.TeacherSubjects).FirstOrDefaultAsync(t => t.Id == id);
    public Task<List<Subject>> GetFormSubjectsAsync(List<int> currentSubjectIds) => _context.Subjects.AsNoTracking()
        .Where(s => s.Status == SubjectStatus.Active || currentSubjectIds.Contains(s.Id)).OrderBy(s => s.Name).ToListAsync();
    public Task<int> CountValidSubjectsAsync(List<int> subjectIds, List<int> currentSubjectIds) => _context.Subjects
        .CountAsync(s => subjectIds.Contains(s.Id) && (s.Status == SubjectStatus.Active || currentSubjectIds.Contains(s.Id)));
    public Task<List<string>> GetBlockingClassesAsync(int teacherId, List<int> removedSubjectIds) => _context.Classes.AsNoTracking()
        .Where(c => c.TeacherId == teacherId && removedSubjectIds.Contains(c.SubjectId)
            && c.Status != ClassStatus.Completed && c.Status != ClassStatus.Cancelled)
        .OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
    public void AddUser(AppUser user) => _context.Users.Add(user);
    public void AddTeacher(TeacherProfile teacher) => _context.TeacherProfiles.Add(teacher);
    public void RemoveSubjects(IEnumerable<TeacherSubject> subjects) => _context.TeacherSubjects.RemoveRange(subjects);
}
