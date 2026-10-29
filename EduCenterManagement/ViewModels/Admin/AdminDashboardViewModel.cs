
namespace EduCenterManagement.ViewModels.Admin
{
    public class AdminDashboardViewModel
    {
        public int TotalTeachers { get; set; }
        public int ActiveTeachers { get; set; }

        public int TotalSubjects { get; set; }
        public int ActiveSubjects { get; set; }

        public int TotalClasses { get; set; }
        public int ClassesWithTeacher { get; set; }
        public int ClassesWithoutTeacher { get; set; }

        public int OpenClasses { get; set; }
        public int CancelledClasses { get; set; }

        public int TotalTimeSlots { get; set; }
        public int ActiveTimeSlots { get; set; }
        public int TotalClassSchedules { get; set; }

        public int TotalSeats { get; set; }
        public int OccupiedSeats { get; set; }

        public int AvailableSeats =>
            Math.Max(TotalSeats - OccupiedSeats, 0);

        public double OccupancyRate =>
            TotalSeats > 0
                ? Math.Round(OccupiedSeats * 100.0 / TotalSeats, 1)
                : 0;

        public List<DashboardClassRow> Classes { get; set; } = new();
    }

    public class DashboardClassRow
    {
        public int ClassId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public decimal Fee { get; set; }

        public List<string> DayLabels { get; set; } = new();
        public string SlotText { get; set; } = "Chưa có lịch";

        public string? TeacherName { get; set; }

        public int Confirmed { get; set; }
        public int Reserved { get; set; }
        public int MaxStudents { get; set; }

        public int Occupied => Confirmed + Reserved;

        public int Available =>
            Math.Max(MaxStudents - Occupied, 0);

        public bool IsFull => Available == 0;

        public double OccupancyRate =>
            MaxStudents > 0
                ? Math.Round(Occupied * 100.0 / MaxStudents, 1)
                : 0;
    }
}
