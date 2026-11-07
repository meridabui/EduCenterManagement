using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Student
{
    public class StudentTimetableRepository : IStudentTimetableRepository
    {
        private readonly ApplicationDbContext _context;

        public StudentTimetableRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StudentProfile?> GetStudentProfileByUserIdAsync(int userId)
        {
            return await _context.StudentProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.UserId == userId);
        }

        public async Task<List<int>> GetPaidClassIdsAsync(int studentId)
        {
            return await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Confirm)
                .Select(e => e.ClassId)
                .ToListAsync();
        }

        public async Task<List<TimeSlot>> GetActiveTimeSlotsAsync()
        {
            return await _context.TimeSlots
                .AsNoTracking()
                .Where(ts => ts.Status == TimeSlotStatus.Active)
                .OrderBy(ts => ts.StartTime)
                .ToListAsync();
        }

        public async Task<List<ClassSession>> GetSessionsByWeekAsync(List<int> classIds, DateOnly startDate, DateOnly endDate)
        {
            return await _context.ClassSessions
                .AsNoTracking()
                .Include(s => s.Class)
                    .ThenInclude(c => c.Subject)
                .Include(s => s.Class)
                    .ThenInclude(c => c.Teacher)
                .Include(s => s.ClassSchedule)
                .Where(s => classIds.Contains(s.ClassId) &&
                            s.SessionDate >= startDate &&
                            s.SessionDate <= endDate)
                .ToListAsync();
        }

        public async Task<Dictionary<int, AttendanceStatus>> GetAttendanceStatusesAsync(int studentId, List<int> sessionIds)
        {
            return await _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && sessionIds.Contains(a.SessionId))
                .ToDictionaryAsync(a => a.SessionId, a => a.Status);
        }

        public async Task<Dictionary<int, (int Count, int FirstSlideId)>> GetSlidesInfoAsync(List<int> sessionIds)
        {
            var raw = await _context.Slides
                .AsNoTracking()
                .Where(sl => sessionIds.Contains(sl.SessionId))
                .GroupBy(sl => sl.SessionId)
                .Select(g => new
                {
                    SessionId = g.Key,
                    Count = g.Count(),
                    FirstSlideId = g.OrderBy(s => s.Id).Select(s => s.Id).FirstOrDefault()
                })
                .ToListAsync();

            return raw.ToDictionary(x => x.SessionId, x => (x.Count, x.FirstSlideId));
        }

        public async Task<Dictionary<int, (int Count, int FirstQuizId)>> GetQuizzesInfoAsync(List<int> sessionIds)
        {
            var raw = await _context.Quizzes
                .AsNoTracking()
                .Where(q => q.SessionId.HasValue && sessionIds.Contains(q.SessionId.Value) && q.Status == QuizStatus.Published)
                .GroupBy(q => q.SessionId!.Value)
                .Select(g => new
                {
                    SessionId = g.Key,
                    Count = g.Count(),
                    FirstQuizId = g.OrderBy(q => q.Id).Select(q => q.Id).FirstOrDefault()
                })
                .ToListAsync();

            return raw.ToDictionary(x => x.SessionId, x => (x.Count, x.FirstQuizId));
        }

        public async Task<HashSet<int>> GetAttemptedQuizIdsAsync(int studentId, List<int> quizIds)
        {
            var attempts = await _context.QuizAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && quizIds.Contains(a.QuizId))
                .Select(a => a.QuizId)
                .Distinct()
                .ToListAsync();

            return new HashSet<int>(attempts);
        }

        public async Task<int> GetUnreadNotificationCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<bool> HasPaidEnrollmentAsync(int studentId, int classId)
        {
            return await _context.Enrollments
                .AnyAsync(e => e.StudentId == studentId &&
                               e.ClassId == classId &&
                               e.Status == EnrollmentStatus.Confirm);
        }

        public async Task<Class?> GetClassWithDetailsAsync(int classId)
        {
            return await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                .FirstOrDefaultAsync(c => c.Id == classId);
        }

        public async Task<List<ClassSession>> GetSessionsByClassIdAsync(int classId)
        {
            return await _context.ClassSessions
                .AsNoTracking()
                .Include(s => s.Class)
                .Where(s => s.ClassId == classId)
                .OrderBy(s => s.SessionNumber)
                .ThenBy(s => s.SessionDate)
                .ToListAsync();
        }

        public async Task<Slide?> GetSlideByIdAsync(int slideId)
        {
            return await _context.Slides
                .Include(s => s.Session)
                .FirstOrDefaultAsync(s => s.Id == slideId);
        }
    }
}
