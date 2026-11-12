namespace EduCenterManagement.ViewModels.Teacher
{
    public class ClassGradebookViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int TotalSessions { get; set; }
        public int TakenSessionsCount { get; set; }

        public List<GradebookQuizHeaderViewModel> Quizzes { get; set; } = new();
        public List<StudentGradebookRowViewModel> Students { get; set; } = new();
    }

    public class GradebookQuizHeaderViewModel
    {
        public int QuizId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal TotalPoints { get; set; }
    }

    public class StudentGradebookRowViewModel
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int ExcusedCount { get; set; }
        public double AttendanceRate { get; set; }

        public Dictionary<int, decimal?> QuizScores { get; set; } = new();
        public decimal? AverageQuizScore10 { get; set; }
        public int CompletedQuizzesCount { get; set; }
    }
}

