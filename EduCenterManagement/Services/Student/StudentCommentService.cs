using EduCenterManagement.Repositories.Student;
using EduCenterManagement.ViewModels.Student;

namespace EduCenterManagement.Services.Student
{
    public class StudentCommentService : IStudentCommentService
    {
        private readonly IStudentCommentRepository _repository;

        public StudentCommentService(IStudentCommentRepository repository)
        {
            _repository = repository;
        }

        public async Task<StudentCommentListViewModel?> GetCommentsAsync(int userId, int? classId)
        {
            var student = await _repository.GetStudentProfileByUserIdAsync(userId);
            if (student == null)
            {
                return null;
            }

            var comments = await _repository.GetVisibleCommentsAsync(student.Id);

            var classes = comments
                .GroupBy(c => new { c.ClassId, c.Class.Name })
                .Select(g => new StudentCommentClassOption
                {
                    ClassId = g.Key.ClassId,
                    ClassName = g.Key.Name,
                    CommentCount = g.Count()
                })
                .OrderBy(c => c.ClassName)
                .ToList();

            // Bỏ bộ lọc nếu lớp được chọn không thuộc danh sách của học sinh
            if (classId.HasValue && classes.All(c => c.ClassId != classId.Value))
            {
                classId = null;
            }

            var items = comments
                .Where(c => !classId.HasValue || c.ClassId == classId.Value)
                .Select(c => new StudentCommentItemViewModel
                {
                    Id = c.Id,
                    Content = c.Content,
                    TeacherName = c.Teacher?.FullName ?? "Giáo viên",
                    ClassId = c.ClassId,
                    ClassName = c.Class.Name,
                    SubjectName = c.Class.Subject?.Name,
                    SessionNumber = c.Session?.SessionNumber,
                    SessionDate = c.Session?.SessionDate,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToList();

            return new StudentCommentListViewModel
            {
                SelectedClassId = classId,
                Classes = classes,
                Comments = items
            };
        }
    }
}
