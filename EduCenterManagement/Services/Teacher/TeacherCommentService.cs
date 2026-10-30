using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Teacher;
using EduCenterManagement.ViewModels.Teacher.Comment;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.Services.Teacher
{
    public class TeacherCommentService : ITeacherCommentService
    {
        private readonly ITeacherCommentRepository _repo;

        public TeacherCommentService(ITeacherCommentRepository repo)
        {
            _repo = repo;
        }

        public async Task<ClassStudentCommentListViewModel?> GetClassStudentCommentListAsync(int userId, int? classId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var classes = await _repo.GetTeacherClassesAsync(teacher.Id);
            var viewModel = new ClassStudentCommentListViewModel
            {
                TeacherName = teacher.FullName,
                SelectedClassId = classId,
                Classes = classes.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Name} ({c.Subject?.Name ?? "Môn học"})",
                    Selected = classId.HasValue && c.Id == classId.Value
                }).ToList()
            };

            int targetClassId = classId ?? (classes.FirstOrDefault()?.Id ?? 0);
            if (targetClassId > 0)
            {
                viewModel.SelectedClassId = targetClassId;
                var classItem = await _repo.GetClassByIdAsync(targetClassId, teacher.Id);
                if (classItem != null)
                {
                    viewModel.ClassId = classItem.Id;
                    viewModel.ClassName = classItem.Name;
                    viewModel.SubjectName = classItem.Subject?.Name ?? "N/A";

                    var enrollments = await _repo.GetConfirmedEnrollmentsByClassIdAsync(targetClassId);
                    var allComments = await _repo.GetCommentsByClassIdAsync(targetClassId);

                    viewModel.Students = enrollments.Select(e =>
                    {
                        var studentComments = allComments.Where(c => c.StudentId == e.StudentId).ToList();
                        var latest = studentComments.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
                        return new StudentCommentSummaryItem
                        {
                            StudentId = e.StudentId,
                            StudentCode = $"HS{e.StudentId:D3}",
                            FullName = e.Student.FullName,
                            DateOfBirth = e.Student.DateOfBirth,
                            CommentCount = studentComments.Count,
                            LatestCommentContent = latest?.Content,
                            LatestCommentAt = latest?.CreatedAt
                        };
                    }).OrderBy(s => s.FullName).ToList();
                }
            }

            return viewModel;
        }

        public async Task<StudentCommentDetailViewModel?> GetStudentCommentDetailAsync(int userId, int classId, int studentId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var classItem = await _repo.GetClassByIdAsync(classId, teacher.Id);
            if (classItem == null) return null;

            var student = await _repo.GetStudentByIdAsync(studentId);
            if (student == null) return null;

            var comments = await _repo.GetCommentsForStudentInClassAsync(studentId, classId);
            var sessions = await _repo.GetSessionsByClassIdAsync(classId);

            var viewModel = new StudentCommentDetailViewModel
            {
                StudentId = student.Id,
                StudentCode = $"HS{student.Id:D3}",
                StudentName = student.FullName,
                DateOfBirth = student.DateOfBirth,
                ClassId = classItem.Id,
                ClassName = classItem.Name,
                SubjectName = classItem.Subject?.Name ?? "N/A",
                TeacherName = teacher.FullName,
                Comments = comments.Select(c => new CommentItemViewModel
                {
                    Id = c.Id,
                    TeacherId = c.TeacherId,
                    TeacherName = teacher.FullName,
                    Content = c.Content,
                    Visibility = c.Visibility,
                    SessionId = c.SessionId,
                    SessionLabel = c.Session != null 
                        ? $"Buổi {c.Session.SessionNumber} ({c.Session.SessionDate:dd/MM/yyyy})" 
                        : "Toàn khóa học",
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                }).ToList(),
                Sessions = sessions.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"Buổi {s.SessionNumber} ({s.SessionDate:dd/MM/yyyy} - {s.StartsAt:HH:mm})"
                }).ToList(),
                NewComment = new CreateCommentInputModel
                {
                    StudentId = student.Id,
                    ClassId = classItem.Id,
                    Visibility = "parent_student"
                }
            };

            return viewModel;
        }

        public async Task<(bool Success, string Message)> CreateCommentAsync(int userId, CreateCommentInputModel model)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.");

            var classItem = await _repo.GetClassByIdAsync(model.ClassId, teacher.Id);
            if (classItem == null) return (false, "Bạn không có quyền nhận xét cho lớp học này.");

            var student = await _repo.GetStudentByIdAsync(model.StudentId);
            if (student == null) return (false, "Không tìm thấy thông tin học sinh.");

            if (string.IsNullOrWhiteSpace(model.Content))
            {
                return (false, "Nội dung nhận xét không được để trống.");
            }

            var comment = new TeacherComment
            {
                TeacherId = teacher.Id,
                StudentId = model.StudentId,
                ClassId = model.ClassId,
                SessionId = model.SessionId > 0 ? model.SessionId : null,
                Content = model.Content.Trim(),
                Visibility = string.IsNullOrWhiteSpace(model.Visibility) ? "parent_student" : model.Visibility,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repo.AddCommentAsync(comment);
            return (true, "Đã gửi nhận xét thành công!");
        }

        public async Task<(bool Success, string Message)> UpdateCommentAsync(int userId, EditCommentInputModel model)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.");

            var comment = await _repo.GetCommentByIdAsync(model.CommentId, teacher.Id);
            if (comment == null) return (false, "Nhận xét không tồn tại hoặc bạn không có quyền sửa.");

            if (string.IsNullOrWhiteSpace(model.Content))
            {
                return (false, "Nội dung nhận xét không được để trống.");
            }

            comment.Content = model.Content.Trim();
            comment.Visibility = string.IsNullOrWhiteSpace(model.Visibility) ? "parent_student" : model.Visibility;
            comment.SessionId = model.SessionId > 0 ? model.SessionId : null;

            await _repo.UpdateCommentAsync(comment);
            return (true, "Cập nhật nhận xét thành công!");
        }

        public async Task<(bool Success, string Message)> DeleteCommentAsync(int userId, int commentId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.");

            var comment = await _repo.GetCommentByIdAsync(commentId, teacher.Id);
            if (comment == null) return (false, "Nhận xét không tồn tại hoặc bạn không có quyền xóa.");

            await _repo.DeleteCommentAsync(comment);
            return (true, "Đã xóa nhận xét thành công!");
        }
    }
}
