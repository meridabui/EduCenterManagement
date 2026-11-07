using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Teacher
{
    public class TeacherSlideRepository : ITeacherSlideRepository
    {
        private readonly ApplicationDbContext _context;

        public TeacherSlideRepository(ApplicationDbContext context)
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

        public async Task<ClassSession?> GetSessionWithClassByIdAsync(int sessionId, int teacherId)
        {
            return await _context.ClassSessions
                .Include(s => s.Class)
                    .ThenInclude(c => c.Subject)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.Class.TeacherId == teacherId);
        }

        public async Task<List<ClassSession>> GetSessionsByClassIdAsync(int classId, int teacherId)
        {
            return await _context.ClassSessions
                .AsNoTracking()
                .Where(s => s.ClassId == classId && s.Class.TeacherId == teacherId)
                .OrderBy(s => s.SessionNumber)
                .ToListAsync();
        }

        public async Task<List<Slide>> GetSlidesBySessionIdAsync(int sessionId)
        {
            return await _context.Slides
                .AsNoTracking()
                .Where(s => s.SessionId == sessionId)
                .OrderByDescending(s => s.UploadedAt)
                .ToListAsync();
        }

        public async Task<Slide?> GetSlideByIdAsync(int slideId, int teacherId)
        {
            return await _context.Slides
                .Include(s => s.Session)
                    .ThenInclude(sess => sess.Class)
                .FirstOrDefaultAsync(s => s.Id == slideId && s.Session.Class.TeacherId == teacherId);
        }

        public async Task AddSlideAsync(Slide slide)
        {
            await _context.Slides.AddAsync(slide);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteSlideAsync(Slide slide)
        {
            _context.Slides.Remove(slide);
            await _context.SaveChangesAsync();
        }
    }
}
