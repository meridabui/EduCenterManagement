using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Services.Admin;

public class AdminTeacherService : IAdminTeacherService
{
    private readonly IAdminTeacherRepository _repository;
    private readonly IAdminUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    public AdminTeacherService(IAdminTeacherRepository repository, IAdminUnitOfWork unitOfWork, IPasswordHasher<AppUser> passwordHasher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }
    public async Task<AdminTeacherIndexData> GetIndexAsync(int page, int? subjectId)
    {
        var subjects = await _repository.GetFilterSubjectsAsync();
        var teachers = await _repository.GetTeachersAsync(subjectId);
        var nameComparer = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: true);
        var sortedTeachers = teachers.OrderBy(t => t.FullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty, nameComparer)
            .ThenBy(t => t.FullName.Trim(), nameComparer).ThenBy(t => t.Id).ToList();
        var pagination = new PaginationViewModel(sortedTeachers.Count, page);
        return new()
        {
            Subjects = subjects, Pagination = pagination,
            Teachers = sortedTeachers.Skip(pagination.Offset).Take(PaginationViewModel.PageSize).ToList()
        };
    }
    public async Task<List<SelectListItem>> GetSubjectsAsync(List<int>? selectedSubjectIds = null, List<int>? currentSubjectIds = null)
    {
        selectedSubjectIds ??= new();
        currentSubjectIds ??= new();
        var subjects = await _repository.GetFormSubjectsAsync(currentSubjectIds);
        return subjects.Select(s => new SelectListItem
        {
            Value = s.Id.ToString(), Text = s.Name + (s.Status == SubjectStatus.Inactive ? " (ngừng hoạt động)" : ""),
            Selected = selectedSubjectIds.Contains(s.Id)
        }).ToList();
    }
    public async Task<AdminResult> CreateAsync(CreateTeacherViewModel model)
    {
        var email = model.Email.Trim().ToLowerInvariant();
        if (await _repository.EmailExistsAsync(email))
            return AdminResult.Invalid("Email này đã được sử dụng.", nameof(model.Email));
        var validation = await ValidateSubjectsAsync(model.SubjectIds, new());
        if (!validation.Success) return validation;
        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var user = new AppUser
            {
                Email = email, Role = UserRole.Teacher, Status = UserStatus.Active,
                EmailVerifiedAt = now, CreatedAt = now, UpdatedAt = now
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
            _repository.AddUser(user);
            await _unitOfWork.SaveChangesAsync();
            _repository.AddTeacher(new TeacherProfile
            {
                UserId = user.Id, FullName = model.FullName.Trim(), CreatedAt = now, UpdatedAt = now,
                TeacherSubjects = model.SubjectIds.Select(id => new TeacherSubject { SubjectId = id }).ToList()
            });
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
            return AdminResult.Ok("Tạo tài khoản Teacher và phân công các môn học thành công.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return AdminResult.Invalid("Không thể tạo tài khoản Teacher. Vui lòng kiểm tra dữ liệu.");
        }
    }
    public async Task<AdminTeacherSubjectsData?> GetAssignSubjectsAsync(int id)
    {
        var teacher = await _repository.GetByIdAsync(id);
        if (teacher == null) return null;
        var ids = teacher.TeacherSubjects.Select(ts => ts.SubjectId).ToList();
        return new()
        {
            TeacherName = teacher.FullName,
            Model = new() { TeacherId = teacher.Id, SubjectIds = ids },
            Subjects = await GetSubjectsAsync(ids, ids)
        };
    }
    public async Task<AdminResult<AdminTeacherSubjectsData>> AssignSubjectsAsync(AssignTeacherSubjectsViewModel model, bool inputValid)
    {
        var teacher = await _repository.GetByIdAsync(model.TeacherId, tracking: true);
        if (teacher == null) return new() { Status = AdminResultStatus.NotFound };
        var currentSubjectIds = teacher.TeacherSubjects.Select(ts => ts.SubjectId).ToList();
        model.SubjectIds = (model.SubjectIds ?? new()).Distinct().ToList();
        var data = new AdminTeacherSubjectsData
        {
            TeacherName = teacher.FullName, Model = model,
            Subjects = await GetSubjectsAsync(model.SubjectIds, currentSubjectIds)
        };
        AdminResult<AdminTeacherSubjectsData> Failed(AdminResult result) => new()
        {
            Status = result.Status, Message = result.Message, Errors = result.Errors, Data = data
        };
        if (!inputValid) return Failed(new() { Status = AdminResultStatus.Invalid });
        var validation = await ValidateSubjectsAsync(model.SubjectIds, currentSubjectIds);
        if (!validation.Success) return Failed(validation);
        var removedSubjectIds = currentSubjectIds.Except(model.SubjectIds).ToList();
        var blockingClasses = await _repository.GetBlockingClassesAsync(teacher.Id, removedSubjectIds);
        if (blockingClasses.Count > 0)
            return Failed(AdminResult.Invalid("Không thể bỏ môn vì giáo viên vẫn phụ trách các lớp: "
                + string.Join(", ", blockingClasses) + ". Hãy phân công lại giáo viên hoặc hoàn tất xử lý các lớp trước.", nameof(model.SubjectIds)));
        _repository.RemoveSubjects(teacher.TeacherSubjects.Where(ts => removedSubjectIds.Contains(ts.SubjectId)).ToList());
        foreach (var subjectId in model.SubjectIds.Except(currentSubjectIds))
            teacher.TeacherSubjects.Add(new TeacherSubject { SubjectId = subjectId });
        teacher.UpdatedAt = DateTime.UtcNow;
        try { await _unitOfWork.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return Failed(AdminResult.Invalid("Không thể cập nhật môn được phân công. Vui lòng tải lại trang và thử lại."));
        }
        return new() { Data = data, Message = "Đã cập nhật các môn được phân công cho giáo viên." };
    }
    private async Task<AdminResult> ValidateSubjectsAsync(List<int> subjectIds, List<int> currentSubjectIds)
    {
        if (subjectIds.Count == 0)
            return AdminResult.Invalid("Vui lòng chọn ít nhất một môn học.", nameof(CreateTeacherViewModel.SubjectIds));
        if (await _repository.CountValidSubjectsAsync(subjectIds, currentSubjectIds) != subjectIds.Count)
            return AdminResult.Invalid("Môn học không tồn tại hoặc đã ngừng hoạt động. Chỉ được thêm môn đang hoạt động.", nameof(CreateTeacherViewModel.SubjectIds));
        return AdminResult.Ok();
    }
}
