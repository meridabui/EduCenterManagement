namespace EduCenterManagement.ViewModels.Teacher.Slide
{
    public class SlideItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? OriginalFileName { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public long? FileSize { get; set; }
        public string? MimeType { get; set; }
        public DateTime UploadedAt { get; set; }

        public string FileExtension
        {
            get
            {
                if (string.IsNullOrEmpty(OriginalFileName)) return string.Empty;
                return System.IO.Path.GetExtension(OriginalFileName).TrimStart('.').ToLowerInvariant();
            }
        }

        public string FormattedFileSize
        {
            get
            {
                if (!FileSize.HasValue || FileSize.Value <= 0) return "0 KB";
                if (FileSize.Value >= 1024 * 1024)
                {
                    return $"{((double)FileSize.Value / (1024 * 1024)):0.##} MB";
                }
                return $"{((double)FileSize.Value / 1024):0.##} KB";
            }
        }
    }

    public class SessionSlideListViewModel
    {
        public int SessionId { get; set; }
        public int SessionNumber { get; set; }
        public DateOnly SessionDate { get; set; }
        public TimeOnly StartsAt { get; set; }
        public TimeOnly EndsAt { get; set; }
        public string TimeRange => $"{StartsAt:HH:mm} - {EndsAt:HH:mm}";

        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;

        public List<SlideItemViewModel> Slides { get; set; } = new();

        public UploadSlideInputModel UploadInput { get; set; } = new();
    }
}
