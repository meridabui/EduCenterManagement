using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Teacher
{
    public class TeacherCommentRepository : ITeacherCommentRepository
    {
        private readonly ApplicationDbContext _context;

        public TeacherCommentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TeacherProfile?> GetTeacherProfileByUserIdAsync(int userId)
        {
            return await _context.TeacherProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);
        }

        public async Task<List<Class>> GetTeacherClassesAsync(int teacherId)
        {
            return await _context.Classes
                .AsNoTracking()
                .Where(c => c.TeacherId == teacherId && c.Status != ClassStatus.Cancelled)
                .Include(c => c.Subject)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Class?> GetClassByIdAsync(int classId, int teacherId)
        {
            return await _context.Classes
                .Include(c => c.Subject)
                .FirstOrDefaultAsync(c => c.Id == classId && c.TeacherId == teacherId);
        }

        public async Task<List<Enrollment>> GetConfirmedEnrollmentsByClassIdAsync(int classId)
        {
            return await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.ClassId == classId &&
                           (e.Status == EnrollmentStatus.Confirm || e.Status == EnrollmentStatus.PendingPayment))
                .Include(e => e.Student)
                .OrderBy(e => e.Student.FullName)
                .ToListAsync();
        }

        public async Task<List<ClassSession>> GetSessionsByClassIdAsync(int classId)
        {
            return await _context.ClassSessions
                .AsNoTracking()
                .Where(s => s.ClassId == classId)
                .OrderBy(s => s.SessionNumber)
                .ToListAsync();
        }

        public async Task<List<TeacherComment>> GetCommentsByClassIdAsync(int classId)
        {
            return await _context.TeacherComments
                .AsNoTracking()
                .Where(c => c.ClassId == classId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<TeacherComment>> GetCommentsForStudentInClassAsync(int studentId, int classId)
        {
            return await _context.TeacherComments
                .AsNoTracking()
                .Where(c => c.StudentId == studentId && c.ClassId == classId)
                .Include(c => c.Teacher)
                .Include(c => c.Session)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<TeacherComment?> GetCommentByIdAsync(int commentId, int teacherId)
        {
            return await _context.TeacherComments
                .Include(c => c.Class)
                .FirstOrDefaultAsync(c => c.Id == commentId && c.TeacherId == teacherId);
        }

        public async Task<StudentProfile?> GetStudentByIdAsync(int studentId)
        {
            return await _context.StudentProfiles
                .FirstOrDefaultAsync(s => s.Id == studentId);
        }

        public async Task AddCommentAsync(TeacherComment comment)
        {
            await _context.TeacherComments.AddAsync(comment);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCommentAsync(TeacherComment comment)
        {
            comment.UpdatedAt = DateTime.UtcNow;
            _context.TeacherComments.Update(comment);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCommentAsync(TeacherComment comment)
        {
            _context.TeacherComments.Remove(comment);
            await _context.SaveChangesAsync();
        }
    }
}
