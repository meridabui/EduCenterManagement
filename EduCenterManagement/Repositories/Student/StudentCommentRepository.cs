using EduCenterManagement.Data;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Student
{
    public class StudentCommentRepository : IStudentCommentRepository
    {
        private const string TeacherOnlyVisibility = "teacher_only";

        private readonly ApplicationDbContext _context;

        public StudentCommentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StudentProfile?> GetStudentProfileByUserIdAsync(int userId)
        {
            return await _context.StudentProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);
        }

        public async Task<List<TeacherComment>> GetVisibleCommentsAsync(int studentId)
        {
            return await _context.TeacherComments
                .AsNoTracking()
                .Where(c => c.StudentId == studentId && c.Visibility != TeacherOnlyVisibility)
                .Include(c => c.Teacher)
                .Include(c => c.Class)
                    .ThenInclude(cl => cl.Subject)
                .Include(c => c.Session)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
    }
}
