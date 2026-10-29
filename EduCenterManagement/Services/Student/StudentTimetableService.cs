using EduCenterManagement.Enums;
using EduCenterManagement.Repositories.Student;
using EduCenterManagement.ViewModels.Student;

namespace EduCenterManagement.Services.Student
{
    public class StudentTimetableService : IStudentTimetableService
    {
        private readonly IStudentTimetableRepository _repository;

        public StudentTimetableService(IStudentTimetableRepository repository)
        {
            _repository = repository;
        }

        public async Task<WeeklyTimetableViewModel?> GetWeeklyTimetableAsync(int userId, int weekOffset)
        {
            var student = await _repository.GetStudentProfileByUserIdAsync(userId);
            if (student == null)
            {
                return null;
            }

            // Tính ngày Thứ 2 của tuần hiện tại hoặc theo offset
            var today = DateOnly.FromDateTime(DateTime.Today);
            int diff = (7 + (int)today.DayOfWeek - (int)System.DayOfWeek.Monday) % 7;
            var currentMonday = today.AddDays(-diff);
            var weekStart = currentMonday.AddDays(weekOffset * 7);
            var weekEnd = weekStart.AddDays(6);

            var daysOfWeek = new List<DateOnly>();
            for (int i = 0; i < 7; i++)
            {
                daysOfWeek.Add(weekStart.AddDays(i));
            }

            // Lấy danh sách ca học
            var timeSlotEntities = await _repository.GetActiveTimeSlotsAsync();
            var timeSlots = timeSlotEntities.Select(ts => new TimeSlotHeaderViewModel
            {
                SlotId = ts.Id,
                Name = ts.Name,
                StartTime = ts.StartTime,
                EndTime = ts.EndTime
            }).ToList();

            // Lấy danh sách lớp đã thanh toán (Paid Enrollment - BR21)
            var paidClassIds = await _repository.GetPaidClassIdsAsync(student.Id);
            var sessions = new List<TimetableSessionItemViewModel>();

            if (paidClassIds.Any())
            {
                var now = DateTime.Now;
                var rawSessions = await _repository.GetSessionsByWeekAsync(paidClassIds, weekStart, weekEnd);
                var sessionIds = rawSessions.Select(s => s.Id).ToList();

                var attendanceDict = await _repository.GetAttendanceStatusesAsync(student.Id, sessionIds);
                var slidesInfo = await _repository.GetSlidesInfoAsync(sessionIds);
                var quizzesInfo = await _repository.GetQuizzesInfoAsync(sessionIds);

                var quizIds = quizzesInfo.Values.Select(q => q.FirstQuizId).ToList();
                var attemptedQuizSet = await _repository.GetAttemptedQuizIdsAsync(student.Id, quizIds);

                foreach (var s in rawSessions)
                {
                    int? matchedSlotId = s.ClassSchedule?.TimeSlotId;
                    if (!matchedSlotId.HasValue)
                    {
                        var foundSlot = timeSlots.FirstOrDefault(ts => ts.StartTime == s.StartsAt);
                        matchedSlotId = foundSlot?.SlotId;
                    }

                    var sessionEndDateTime = s.SessionDate.ToDateTime(s.EndsAt);
                    bool hasTakenPlace = now >= sessionEndDateTime;

                    attendanceDict.TryGetValue(s.Id, out var attStatus);

                    slidesInfo.TryGetValue(s.Id, out var slideData);
                    quizzesInfo.TryGetValue(s.Id, out var quizData);

                    int slideCount = slideData.Count;
                    int? firstSlideId = slideCount > 0 ? slideData.FirstSlideId : null;

                    int quizCount = quizData.Count;
                    int? firstQuizId = quizCount > 0 ? quizData.FirstQuizId : null;

                    bool isQuizTaken = firstQuizId.HasValue && attemptedQuizSet.Contains(firstQuizId.Value);

                    sessions.Add(new TimetableSessionItemViewModel
                    {
                        SessionId = s.Id,
                        ClassId = s.ClassId,
                        ClassName = s.Class.Name,
                        SubjectName = s.Class.Subject.Name,
                        TeacherName = s.Class.Teacher?.FullName ?? "Chưa phân công",
                        SessionDate = s.SessionDate,
                        StartsAt = s.StartsAt,
                        EndsAt = s.EndsAt,
                        SessionNumber = s.SessionNumber,
                        Status = s.Status,
                        MatchedTimeSlotId = matchedSlotId,
                        AttendanceStatus = attStatus != 0 ? attStatus : null,
                        HasTakenPlace = hasTakenPlace,
                        SlideCount = slideCount,
                        FirstSlideId = firstSlideId,
                        QuizCount = quizCount,
                        FirstQuizId = firstQuizId,
                        IsQuizTaken = isQuizTaken
                    });
                }
            }

            var unreadCount = await _repository.GetUnreadNotificationCountAsync(userId);

            return new WeeklyTimetableViewModel
            {
                WeekStartDate = weekStart,
                WeekEndDate = weekEnd,
                StudentName = student.FullName,
                UnreadNotificationCount = unreadCount,
                DaysOfWeek = daysOfWeek,
                TimeSlots = timeSlots,
                Sessions = sessions
            };
        }

        public async Task<ClassDetailViewModel?> GetClassDetailAsync(int userId, int classId)
        {
            var student = await _repository.GetStudentProfileByUserIdAsync(userId);
            if (student == null)
            {
                return null;
            }

            // Kiểm tra BR21: Học sinh chỉ xem lớp đã Paid
            var isPaid = await _repository.HasPaidEnrollmentAsync(student.Id, classId);
            if (!isPaid)
            {
                return null;
            }

            var classEntity = await _repository.GetClassWithDetailsAsync(classId);
            if (classEntity == null)
            {
                return null;
            }

            var sessionEntities = await _repository.GetSessionsByClassIdAsync(classId);
            var sessionIds = sessionEntities.Select(s => s.Id).ToList();

            var slidesInfo = await _repository.GetSlidesInfoAsync(sessionIds);

            var sessions = sessionEntities.Select(s =>
            {
                var slides = new List<SlideItemViewModel>();
                // Nếu có slide trong session
                if (slidesInfo.TryGetValue(s.Id, out var slideData) && slideData.Count > 0)
                {
                    slides.Add(new SlideItemViewModel
                    {
                        SlideId = slideData.FirstSlideId,
                        Title = $"Bài giảng Buổi {s.SessionNumber}",
                        OriginalFileName = $"Slide_Buoi_{s.SessionNumber}.pdf",
                        UploadedAt = s.CreatedAt
                    });
                }

                return new SessionMaterialViewModel
                {
                    SessionId = s.Id,
                    SessionNumber = s.SessionNumber,
                    SessionDate = s.SessionDate,
                    StartsAt = s.StartsAt,
                    EndsAt = s.EndsAt,
                    Status = s.Status,
                    Slides = slides
                };
            }).ToList();

            return new ClassDetailViewModel
            {
                ClassId = classEntity.Id,
                ClassName = classEntity.Name,
                SubjectName = classEntity.Subject.Name,
                SubjectDescription = classEntity.Subject.Description,
                TeacherName = classEntity.Teacher?.FullName ?? "Chưa phân công",
                StartDate = classEntity.StartDate,
                EndDate = classEntity.EndDate,
                Fee = classEntity.Fee,
                Status = classEntity.Status,
                Sessions = sessions
            };
        }

        public async Task<(byte[]? FileContent, string? PhysicalPath, string ContentType, string DownloadFileName)> GetSlideDownloadAsync(int userId, int slideId, string webRootPath)
        {
            var student = await _repository.GetStudentProfileByUserIdAsync(userId);
            if (student == null)
            {
                return (null, null, "", "");
            }

            var slide = await _repository.GetSlideByIdAsync(slideId);
            if (slide == null)
            {
                return (null, null, "", "");
            }

            // Kiểm tra quyền Paid Enrollment của lớp
            var isPaid = await _repository.HasPaidEnrollmentAsync(student.Id, slide.Session.ClassId);
            if (!isPaid)
            {
                return (null, null, "", "");
            }

            string filePath = "";
            if (!string.IsNullOrEmpty(slide.FileUrl))
            {
                var relative = slide.FileUrl.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
                filePath = Path.Combine(webRootPath, relative);
            }

            var downloadName = !string.IsNullOrEmpty(slide.OriginalFileName) ? slide.OriginalFileName : $"{slide.Title}.pdf";
            var contentType = slide.MimeType ?? "application/octet-stream";

            if (!File.Exists(filePath))
            {
                // Fallback nếu file chưa upload thực tế
                var fallbackContent = System.Text.Encoding.UTF8.GetBytes($"Tài liệu: {slide.Title}\nBuổi học: {slide.SessionId}\nTải lúc: {DateTime.Now}");
                return (fallbackContent, null, "text/plain", $"{slide.Title}.txt");
            }

            return (null, filePath, contentType, downloadName);
        }
    }
}
