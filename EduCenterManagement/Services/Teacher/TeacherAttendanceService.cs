using EduCenterManagement.Enums;
using EduCenterManagement.Repositories.Teacher;
using EduCenterManagement.ViewModels.Teacher.Attendance;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.Services.Teacher
{
    public class TeacherAttendanceService : ITeacherAttendanceService
    {
        private readonly ITeacherAttendanceRepository _repo;

        public TeacherAttendanceService(ITeacherAttendanceRepository repo)
        {
            _repo = repo;
        }

        public async Task<ClassSessionPickerViewModel?> GetPickerViewModelAsync(int userId, int? classId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var classes = await _repo.GetTeacherClassesAsync(teacher.Id);

            var model = new ClassSessionPickerViewModel
            {
                TeacherName = teacher.FullName,
                Classes = classes.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Name} ({c.Subject?.Name ?? "Môn học"})",
                    Selected = classId.HasValue && classId.Value == c.Id
                }).ToList()
            };

            int targetClassId = classId ?? (classes.FirstOrDefault()?.Id ?? 0);
            if (targetClassId > 0)
            {
                model.SelectedClassId = targetClassId;
                var sessions = await _repo.GetSessionsByClassIdAsync(targetClassId, teacher.Id);
                model.Sessions = sessions.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"Buổi {s.SessionNumber} ({s.SessionDate:dd/MM/yyyy} - {s.StartsAt:HH:mm} đến {s.EndsAt:HH:mm})"
                }).ToList();
            }

            return model;
        }

        public async Task<AttendanceSheetViewModel?> GetAttendanceSheetAsync(int userId, int sessionId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var session = await _repo.GetSessionWithClassByIdAsync(sessionId, teacher.Id);
            if (session == null) return null;

            var enrollments = await _repo.GetConfirmedEnrollmentsByClassIdAsync(session.ClassId);
            var existingAttendance = await _repo.GetAttendanceRecordsBySessionIdAsync(sessionId);

            var studentItems = enrollments.Select(e =>
            {
                var record = existingAttendance.FirstOrDefault(a => a.StudentId == e.StudentId);
                bool hasRecord = record != null;

                return new StudentAttendanceItemViewModel
                {
                    StudentId = e.StudentId,
                    StudentCode = $"HS{e.Student.Id:D3}",
                    FullName = e.Student.FullName,
                    DateOfBirth = e.Student.DateOfBirth,
                    // Quy tắc: Nếu đã từng lưu thì lấy trạng thái đã lưu; nếu chưa từng điểm danh thì mặc định là Absent (Vắng)
                    Status = hasRecord ? record!.Status : AttendanceStatus.Absent,
                    Note = record?.Note,
                    HasExistingRecord = hasRecord
                };
            }).ToList();

            return new AttendanceSheetViewModel
            {
                SessionId = session.Id,
                SessionNumber = session.SessionNumber,
                SessionDate = session.SessionDate,
                DayOfWeekName = GetDayOfWeekName(session.SessionDate.DayOfWeek == System.DayOfWeek.Sunday ? 8 : (int)session.SessionDate.DayOfWeek + 1),
                StartsAt = session.StartsAt,
                EndsAt = session.EndsAt,
                SessionStatus = session.Status,
                ClassId = session.ClassId,
                ClassName = session.Class.Name,
                SubjectName = session.Class.Subject?.Name ?? "Chưa xác định",
                TeacherName = teacher.FullName,
                MarkSessionAsCompleted = session.Status == ClassSessionStatus.Scheduled,
                Students = studentItems
            };
        }

        public async Task<(bool Success, string Message)> SaveAttendanceSheetAsync(int userId, AttendanceSheetViewModel model)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.");

            var session = await _repo.GetSessionWithClassByIdAsync(model.SessionId, teacher.Id);
            if (session == null) return (false, "Không tìm thấy buổi học hoặc bạn không phụ trách lớp này.");

            var records = model.Students.Select(s => (s.StudentId, s.Status, s.Note)).ToList();

            await _repo.SaveAttendanceRecordsAsync(
                model.SessionId,
                teacher.Id,
                records,
                model.MarkSessionAsCompleted);

            int presentCount = records.Count(r => r.Status == AttendanceStatus.Present);
            int absentCount = records.Count(r => r.Status == AttendanceStatus.Absent);

            return (true, $"Lưu điểm danh Buổi {session.SessionNumber} thành công! (Có mặt: {presentCount}, Vắng: {absentCount})");
        }

        public async Task<List<object>> GetSessionsByClassAsync(int userId, int classId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return new List<object>();

            var sessions = await _repo.GetSessionsByClassIdAsync(classId, teacher.Id);
            return sessions.Select(s => (object)new
            {
                id = s.Id,
                text = $"Buổi {s.SessionNumber} ({s.SessionDate:dd/MM/yyyy} - {s.StartsAt:HH:mm} đến {s.EndsAt:HH:mm})"
            }).ToList();
        }

        private static string GetDayOfWeekName(int day) => day switch
        {
            2 => "Thứ Hai",
            3 => "Thứ Ba",
            4 => "Thứ Tư",
            5 => "Thứ Năm",
            6 => "Thứ Sáu",
            7 => "Thứ Bảy",
            8 => "Chủ Nhật",
            _ => $"Thứ {day}"
        };
    }
}

