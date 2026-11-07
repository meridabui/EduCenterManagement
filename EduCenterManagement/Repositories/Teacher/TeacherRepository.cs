using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Teacher
{
    public class TeacherRepository : ITeacherRepository
    {
        private readonly ApplicationDbContext _context;

        public TeacherRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TeacherProfile?> GetTeacherProfileByUserIdAsync(int userId)
        {
            return await _context.TeacherProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);
        }

        public async Task<List<Class>> GetAssignedClassesAsync(int teacherId)
        {
            return await _context.Classes
                .AsNoTracking()
                .Where(c => c.TeacherId == teacherId && c.Status != ClassStatus.Cancelled)
                .Include(c => c.Subject)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ClassSchedule>> GetClassSchedulesAsync(List<int> classIds)
        {
            return await _context.ClassSchedules
                .AsNoTracking()
                .Where(s => classIds.Contains(s.ClassId))
                .Include(s => s.TimeSlot)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.TimeSlot.StartTime)
                .ToListAsync();
        }

        public async Task<Dictionary<int, int>> GetEnrollmentCountsAsync(List<int> classIds)
        {
            return await _context.Enrollments
                .AsNoTracking()
                .Where(e => classIds.Contains(e.ClassId) &&
                           (e.Status == EnrollmentStatus.Confirm || e.Status == EnrollmentStatus.PendingPayment))
                .GroupBy(e => e.ClassId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassId, x => x.Count);
        }

        public async Task<Class?> GetClassByIdAndTeacherAsync(int classId, int teacherId)
        {
            return await _context.Classes
                .Include(c => c.Subject)
                .FirstOrDefaultAsync(c => c.Id == classId && c.TeacherId == teacherId);
        }

        public async Task<List<Enrollment>> GetConfirmedStudentsByClassIdAsync(int classId)
        {
            return await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.ClassId == classId &&
                           (e.Status == EnrollmentStatus.Confirm || e.Status == EnrollmentStatus.PendingPayment))
                .Include(e => e.Student)
                .OrderBy(e => e.Student.FullName)
                .ToListAsync();
        }

        public async Task<List<TimeSlot>> GetActiveTimeSlotsAsync()
        {
            return await _context.TimeSlots
                .AsNoTracking()
                .Where(t => t.Status == TimeSlotStatus.Active)
                .OrderBy(t => t.StartTime)
                .ToListAsync();
        }

        public async Task<List<Class>> GetClassesInWeekRangeAsync(int teacherId, DateOnly weekStart, DateOnly weekEnd)
        {
            return await _context.Classes
                .AsNoTracking()
                .Where(c => c.TeacherId == teacherId &&
                            c.Status != ClassStatus.Cancelled &&
                            c.StartDate <= weekEnd &&
                            c.EndDate >= weekStart)
                .Include(c => c.Subject)
                .ToListAsync();
        }

        public async Task<List<ClassSession>> GetActualSessionsInWeekRangeAsync(List<int> classIds, DateOnly weekStart, DateOnly weekEnd)
        {
            return await _context.ClassSessions
                .AsNoTracking()
                .Where(s => classIds.Contains(s.ClassId) &&
                            s.SessionDate >= weekStart &&
                            s.SessionDate <= weekEnd)
                .Include(s => s.Class)
                    .ThenInclude(c => c.Subject)
                .Include(s => s.ClassSchedule)
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

        public async Task<List<ClassSchedule>> GetSchedulesByClassIdAsync(int classId)
        {
            return await _context.ClassSchedules
                .AsNoTracking()
                .Where(cs => cs.ClassId == classId)
                .Include(cs => cs.TimeSlot)
                .Include(cs => cs.Class)
                    .ThenInclude(c => c.Subject)
                .ToListAsync();
        }

        public async Task<bool> HasSessionsAsync(int classId)
        {
            return await _context.ClassSessions.AnyAsync(s => s.ClassId == classId);
        }

        public async Task AddSessionsRangeAsync(List<ClassSession> sessions)
        {
            _context.ClassSessions.AddRange(sessions);
            await _context.SaveChangesAsync();
        }

        public async Task<ClassSession?> GetSessionByIdAndTeacherAsync(int sessionId, int teacherId)
        {
            return await _context.ClassSessions
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.Class.TeacherId == teacherId);
        }

        public async Task<bool> UpdateSessionStatusAsync(int sessionId, int teacherId, ClassSessionStatus status)
        {
            var session = await _context.ClassSessions
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.Class.TeacherId == teacherId);

            if (session == null) return false;

            session.Status = status;
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}

