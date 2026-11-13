using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Admin;

public class AdminClassRepository : IAdminClassRepository
{
    private readonly ApplicationDbContext _context;
    public AdminClassRepository(ApplicationDbContext context) => _context = context;

    public Task<Class?> GetByIdAsync(int id) => _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    public Task<Class?> GetWithSchedulesAsync(int id, bool tracking = false, bool includeSlots = false)
    {
        var query = (tracking ? _context.Classes : _context.Classes.AsNoTracking()).Include(c => c.ClassSchedules).AsQueryable();
        if (includeSlots) query = query.Include(c => c.ClassSchedules).ThenInclude(s => s.TimeSlot);
        return query.FirstOrDefaultAsync(c => c.Id == id);
    }
    public Task<Class?> GetDetailsAsync(int id) => _context.Classes.AsNoTracking()
        .Include(c => c.Subject).Include(c => c.Teacher).ThenInclude(t => t!.User)
        .Include(c => c.ClassSchedules).ThenInclude(s => s.TimeSlot).FirstOrDefaultAsync(c => c.Id == id);
    public Task<Class?> GetDeleteAsync(int id) => _context.Classes.AsNoTracking()
        .Include(c => c.Subject).Include(c => c.Teacher).FirstOrDefaultAsync(c => c.Id == id);

    private IQueryable<Class> FilterClasses(int? subjectId, int? teacherId, string? status)
    {
        var query = _context.Classes.AsNoTracking().AsQueryable();
        if (subjectId.HasValue) query = query.Where(c => c.SubjectId == subjectId.Value);
        if (teacherId.HasValue) query = query.Where(c => c.TeacherId == teacherId.Value);
        if (status == "Open,InProgress") query = query.Where(c => c.Status == ClassStatus.Open || c.Status == ClassStatus.InProgress);
        else if (Enum.TryParse<ClassStatus>(status, out var selectedStatus)) query = query.Where(c => c.Status == selectedStatus);
        return query;
    }
    public Task<int> CountAsync(int? subjectId, int? teacherId, string? status) => FilterClasses(subjectId, teacherId, status).CountAsync();
    public Task<List<Class>> GetPageAsync(int? subjectId, int? teacherId, string? status, int offset, int pageSize)
        => FilterClasses(subjectId, teacherId, status).AsNoTracking().Include(c => c.Subject).Include(c => c.Teacher)
            .Include(c => c.ClassSchedules).ThenInclude(s => s.TimeSlot).OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id).Skip(offset).Take(pageSize).ToListAsync();
    public Task<List<Subject>> GetSubjectsAsync(bool activeOnly = false)
        => _context.Subjects.AsNoTracking().Where(s => !activeOnly || s.Status == SubjectStatus.Active).OrderBy(s => s.Name).ToListAsync();
    public Task<List<TeacherProfile>> GetTeachersAsync(int? subjectId = null, bool activeOnly = false)
    {
        var query = _context.TeacherProfiles.AsNoTracking();
        if (activeOnly) query = query.Where(t => t.User.Status == UserStatus.Active && t.User.Role == UserRole.Teacher);
        if (subjectId.HasValue) query = query.Where(t => t.TeacherSubjects.Any(s => s.SubjectId == subjectId.Value));
        return query.OrderBy(t => t.FullName).ToListAsync();
    }
    public Task<List<TimeSlot>> GetActiveSlotsAsync() => _context.TimeSlots.AsNoTracking()
        .Where(t => t.Status == TimeSlotStatus.Active).OrderBy(t => t.StartTime).ToListAsync();
    public Task<Dictionary<int, TimeSlot>> GetSlotsAsync(List<int> ids, bool tracking = true)
        => (tracking ? _context.TimeSlots : _context.TimeSlots.AsNoTracking()).Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
    public Task<int> CountActiveSlotsAsync(List<int> ids) => _context.TimeSlots.CountAsync(t => ids.Contains(t.Id) && t.Status == TimeSlotStatus.Active);
    public Task<bool> NameExistsAsync(string name, int exceptId) => _context.Classes.AnyAsync(c => c.Name == name && c.Id != exceptId);
    public Task<bool> IsSubjectActiveAsync(int id) => _context.Subjects.AnyAsync(s => s.Id == id && s.Status == SubjectStatus.Active);
    public Task<bool> IsActiveTeacherForSubjectAsync(int teacherId, int subjectId) => _context.TeacherProfiles.AnyAsync(t => t.Id == teacherId
        && t.TeacherSubjects.Any(ts => ts.SubjectId == subjectId) && t.User.Status == UserStatus.Active && t.User.Role == UserRole.Teacher);
    public Task<List<AdminScheduleConflict>> FindConflictsAsync(int teacherId, int? currentClassId,
        DateOnly start, DateOnly end, List<int> days, TimeOnly startTime, TimeOnly endTime)
        => _context.ClassSchedules.AsNoTracking().Where(s => s.Class.TeacherId == teacherId
            && (!currentClassId.HasValue || s.ClassId != currentClassId.Value)
            && s.Class.Status != ClassStatus.Cancelled && s.Class.Status != ClassStatus.Completed
            && s.Class.StartDate <= end && s.Class.EndDate >= start && days.Contains(s.DayOfWeek)
            && s.TimeSlot.StartTime < endTime && s.TimeSlot.EndTime > startTime)
            .Select(s => new AdminScheduleConflict(s.Class.Name, s.DayOfWeek, s.TimeSlot.Name)).ToListAsync();
    public Task<List<ClassSchedule>> GetOtherSchedulesAsync(int id, int? teacherId, DateOnly start, DateOnly end)
        => _context.ClassSchedules.AsNoTracking().Include(s => s.TimeSlot)
            .Where(s => s.ClassId != id && s.Class.TeacherId == teacherId
                && s.Class.Status != ClassStatus.Cancelled && s.Class.Status != ClassStatus.Completed
                && s.Class.StartDate <= end && s.Class.EndDate >= start).ToListAsync();
    public Task<List<AdminCapacityStudent>> GetCapacityStudentsAsync(int id, DateTime now) => _context.Enrollments.AsNoTracking()
        .Where(e => e.ClassId == id && (e.Status == EnrollmentStatus.Confirm || (e.Status == EnrollmentStatus.PendingPayment
            && (e.ReservedUntil == null || e.ReservedUntil > now)))).OrderBy(e => e.Status).ThenBy(e => e.ReservedUntil)
        .Select(e => new AdminCapacityStudent(e.Student.FullName, e.RegisteredByParentId, e.ReservedUntil,
            e.Status == EnrollmentStatus.Confirm ? "Confirmed" : "Reserved")).ToListAsync();
    public Task<int> CountSessionsAsync(int id) => _context.ClassSessions.CountAsync(s => s.ClassId == id);
    public Task<bool> HasConfirmedEnrollmentsAsync(int id) => _context.Enrollments.AnyAsync(e => e.ClassId == id && e.Status == EnrollmentStatus.Confirm);
    public Task<bool> HasPaidPaymentsAsync(int id) => _context.Payments.AnyAsync(p => p.Enrollment.ClassId == id && p.Status == PaymentStatus.Paid);
    public Task<bool> HasSessionsAsync(int id) => _context.ClassSessions.AnyAsync(s => s.ClassId == id);
    public Task<bool> HasUnfinishedSessionsAsync(int id) => _context.ClassSessions.AnyAsync(s => s.ClassId == id
        && s.Status != ClassSessionStatus.Completed && s.Status != ClassSessionStatus.Cancelled);
    public Task<int> LockDraftAsync(int id) => _context.Classes.Where(c => c.Id == id && c.Status == ClassStatus.Draft)
        .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, c => c.UpdatedAt));
    public Task<int> LockForAssignmentAsync(int id) => _context.Classes.Where(c => c.Id == id && (c.Status == ClassStatus.Draft || c.Status == ClassStatus.Open))
        .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, c => c.UpdatedAt));
    public Task<int> LockForTransitionAsync(int id, ClassStatus source, ClassStatus target) => _context.Classes.Where(c => c.Id == id
        && (c.Status == source || (target == ClassStatus.Cancelled && c.Status == ClassStatus.Draft)))
        .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, c => c.UpdatedAt));
    public void AddClass(Class classItem) => _context.Classes.Add(classItem);
    public void AddSchedule(ClassSchedule schedule) => _context.ClassSchedules.Add(schedule);
    public void AddSchedules(IEnumerable<ClassSchedule> schedules) => _context.ClassSchedules.AddRange(schedules);
    public void RemoveSchedules(IEnumerable<ClassSchedule> schedules) => _context.ClassSchedules.RemoveRange(schedules);
    public Task<List<ClassSession>> GetSessionsAsync(int classId) => _context.ClassSessions.Where(s => s.ClassId == classId).ToListAsync();
    public Task<List<int>> GetReferencedSessionIdsAsync(int classId) => _context.AttendanceRecords
        .Where(a => a.Session.ClassId == classId).Select(a => a.SessionId)
        .Union(_context.Slides.Where(s => s.Session.ClassId == classId).Select(s => s.SessionId))
        .Union(_context.Quizzes.Where(q => q.SessionId != null && q.Session!.ClassId == classId).Select(q => q.SessionId!.Value))
        .Union(_context.TeacherComments.Where(c => c.SessionId != null && c.Session!.ClassId == classId).Select(c => c.SessionId!.Value)).ToListAsync();
    public void RemoveSession(ClassSession session) => _context.ClassSessions.Remove(session);
    public void AddSessions(IEnumerable<ClassSession> sessions) => _context.ClassSessions.AddRange(sessions);
    public Task<List<int>> GetCancellationRecipientsAsync(int id, int? teacherId) => _context.Enrollments
        .Where(e => e.ClassId == id && e.Status == EnrollmentStatus.PendingPayment).Select(e => e.RegisteredByParent.UserId)
        .Union(_context.Enrollments.Where(e => e.ClassId == id && e.Status == EnrollmentStatus.PendingPayment).Select(e => e.Student.UserId))
        .Union(_context.TeacherProfiles.Where(t => t.Id == teacherId).Select(t => t.UserId)).ToListAsync();
    public void AddNotifications(IEnumerable<Notification> notifications) => _context.Notifications.AddRange(notifications);
    public Task CancelPendingEnrollmentsAsync(int id, DateTime now) => _context.Enrollments
        .Where(e => e.ClassId == id && e.Status == EnrollmentStatus.PendingPayment)
        .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EnrollmentStatus.Cancelled)
            .SetProperty(e => e.CancelledAt, (DateTime?)now).SetProperty(e => e.UpdatedAt, now));
    public Task CancelUnfinishedSessionsAsync(int id, DateTime now) => _context.ClassSessions
        .Where(s => s.ClassId == id && s.Status != ClassSessionStatus.Completed && s.Status != ClassSessionStatus.Cancelled)
        .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, ClassSessionStatus.Cancelled).SetProperty(e => e.UpdatedAt, now));
}
