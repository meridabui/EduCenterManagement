
namespace EduCenterManagement.ViewModels.Admin
{
    public class AdminDashboardViewModel
    {
        public int TotalTeachers { get; set; }
        public int ActiveTeachers { get; set; }

        public int TotalSubjects { get; set; }
        public int ActiveSubjects { get; set; }

        public int TotalClasses { get; set; }
        public int OpenClasses { get; set; }
        public int ClassesWithoutTeacher { get; set; }
        public int CancelledClasses { get; set; }

        public int TotalTimeSlots { get; set; }
        public int ActiveTimeSlots { get; set; }

        public int TotalClassSchedules { get; set; }
    }
}
