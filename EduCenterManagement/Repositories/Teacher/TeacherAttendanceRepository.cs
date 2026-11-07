using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Teacher
{
    public class TeacherAttendanceRepository : ITeacherAttendanceRepository
    {
        private readonly ApplicationDbContext _context;

        public TeacherAttendanceRepository(ApplicationDbContext context)
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

        public async Task<List<AttendanceRecord>> GetAttendanceRecordsBySessionIdAsync(int sessionId)
        {
            return await _context.AttendanceRecords
                .Where(a => a.SessionId == sessionId)
                .ToListAsync();
        }

        public async Task SaveAttendanceRecordsAsync(
            int sessionId,
            int teacherId,
            List<(int StudentId, AttendanceStatus Status, string? Note)> records,
            bool markSessionCompleted)
        {
            var existingRecords = await _context.AttendanceRecords
                .Where(a => a.SessionId == sessionId)
                .ToListAsync();

            var now = DateTime.UtcNow;

            foreach (var item in records)
            {
                var existing = existingRecords.FirstOrDefault(a => a.StudentId == item.StudentId);
                if (existing != null)
                {
                    existing.Status = item.Status;
                    existing.Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim();
                    existing.MarkedByTeacherId = teacherId;
                    existing.UpdatedAt = now;
                }
                else
                {
                    var newRecord = new AttendanceRecord
                    {
                        SessionId = sessionId,
                        StudentId = item.StudentId,
                        MarkedByTeacherId = teacherId,
                        Status = item.Status,
                        Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim(),
                        MarkedAt = now,
                        UpdatedAt = now
                    };
                    _context.AttendanceRecords.Add(newRecord);
                }
            }

            if (markSessionCompleted)
            {
                var session = await _context.ClassSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
                if (session != null && session.Status == ClassSessionStatus.Scheduled)
                {
                    session.Status = ClassSessionStatus.Completed;
                    session.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}

