using EduCenterManagement.Enums;
using EduCenterManagement.Repositories.Admin;
using EduCenterManagement.ViewModels.Admin;

namespace EduCenterManagement.Services.Admin;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly IAdminDashboardRepository _repository;
    public AdminDashboardService(IAdminDashboardRepository repository) => _repository = repository;

    public async Task<AdminDashboardViewModel> GetDashboardAsync()
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Now);

        var classes = await _repository.GetClassesAsync();

        var classIds = classes.Select(c => c.Id).ToList();

        var enrollmentCounts = await _repository.GetEnrollmentCountsAsync(classIds, now);

        int CountOf(int classId, EnrollmentStatus status) =>
            enrollmentCounts
                .Where(x => x.ClassId == classId &&
                            x.Status == status)
                .Select(x => x.Count)
                .FirstOrDefault();

        var rows = classes.Select(c =>
        {
            var schedules = c.ClassSchedules
                .OrderBy(s => s.DayOfWeek)
                .ToList();

            var firstSchedule = schedules.FirstOrDefault();

            return new DashboardClassRow
            {
                ClassId = c.Id,
                StartDate = c.StartDate,
                Name = c.Name,
                SubjectName = c.Subject?.Name ?? "Chưa có môn học",
                Fee = c.Fee,

                DayLabels = schedules
                    .Select(s => s.DayOfWeek == 8
                        ? "CN"
                        : $"Thứ {s.DayOfWeek}")
                    .Distinct()
                    .ToList(),

                SlotText = firstSchedule?.TimeSlot is { } slot
                    ? $"{slot.Name} ({slot.StartTime:HH\\:mm} - " +
                      $"{slot.EndTime:HH\\:mm})"
                    : "Chưa có lịch",

                TeacherName = c.Teacher?.FullName,

                Confirmed = CountOf(c.Id, EnrollmentStatus.Confirm),
                Reserved = CountOf(c.Id, EnrollmentStatus.PendingPayment),
                MaxStudents = c.MaxStudents
            };
        }).ToList();

        var rowsById = rows.ToDictionary(r => r.ClassId);
        var upcomingClasses = classes
            .Where(c => c.Status == ClassStatus.Open && c.StartDate >= today)
            .OrderBy(c => c.StartDate)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Take(6)
            .Select(c => rowsById[c.Id])
            .ToList();

        var model = new AdminDashboardViewModel
        {
            TotalTeachers = await _repository.CountTeachersAsync(),

            ActiveTeachers = await _repository.CountTeachersAsync(activeOnly: true),

            TotalSubjects = await _repository.CountSubjectsAsync(),

            ActiveSubjects = await _repository.CountSubjectsAsync(activeOnly: true),

            TotalClasses = rows.Count,

            ClassesWithTeacher = rows.Count(r =>
                !string.IsNullOrWhiteSpace(r.TeacherName)),

            ClassesWithoutTeacher = rows.Count(r =>
                string.IsNullOrWhiteSpace(r.TeacherName)),

            OpenClasses = classes.Count(c =>
                c.Status == ClassStatus.Open),
            InProgressClasses = classes.Count(c =>
                c.Status == ClassStatus.InProgress),

            CancelledClasses = await _repository.CountCancelledClassesAsync(),

            TotalTimeSlots = await _repository.CountTimeSlotsAsync(),

            ActiveTimeSlots = await _repository.CountTimeSlotsAsync(activeOnly: true),

            TotalClassSchedules =
                await _repository.CountSchedulesAsync(),

            TotalSeats = rows.Sum(r => r.MaxStudents),
            OccupiedSeats = rows.Sum(r => r.Occupied),

            Classes = rows,
            UpcomingClasses = upcomingClasses
        };

        return model;
    }
}
