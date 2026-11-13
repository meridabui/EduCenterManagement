using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Admin;

public class AdminDashboardRepository : IAdminDashboardRepository
{
    private readonly ApplicationDbContext _context;
    public AdminDashboardRepository(ApplicationDbContext context) => _context = context;
    public Task<List<Class>> GetClassesAsync() => _context.Classes.AsNoTracking()
        .Where(c => c.Status != ClassStatus.Cancelled).Include(c => c.Subject).Include(c => c.Teacher)
        .Include(c => c.ClassSchedules).ThenInclude(s => s.TimeSlot).OrderBy(c => c.Name).ToListAsync();
    public Task<List<AdminEnrollmentCount>> GetEnrollmentCountsAsync(List<int> classIds, DateTime now)
        => _context.Enrollments.AsNoTracking().Where(e => classIds.Contains(e.ClassId)
            && (e.Status == EnrollmentStatus.Confirm || (e.Status == EnrollmentStatus.PendingPayment
                && (e.ReservedUntil == null || e.ReservedUntil > now))))
            .GroupBy(e => new { e.ClassId, e.Status })
            .Select(g => new AdminEnrollmentCount(g.Key.ClassId, g.Key.Status, g.Count())).ToListAsync();
    public Task<int> CountTeachersAsync(bool activeOnly = false) => activeOnly
        ? _context.TeacherProfiles.CountAsync(t => t.User.Status == UserStatus.Active && t.User.Role == UserRole.Teacher)
        : _context.TeacherProfiles.CountAsync();
    public Task<int> CountSubjectsAsync(bool activeOnly = false) => activeOnly
        ? _context.Subjects.CountAsync(s => s.Status == SubjectStatus.Active) : _context.Subjects.CountAsync();
    public Task<int> CountCancelledClassesAsync() => _context.Classes.CountAsync(c => c.Status == ClassStatus.Cancelled);
    public Task<int> CountTimeSlotsAsync(bool activeOnly = false) => activeOnly
        ? _context.TimeSlots.CountAsync(t => t.Status == TimeSlotStatus.Active) : _context.TimeSlots.CountAsync();
    public Task<int> CountSchedulesAsync() => _context.ClassSchedules.CountAsync();
}
