using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Services.Admin;

public class AdminSubjectService : IAdminSubjectService
{
    private readonly IAdminSubjectRepository _repository;
    private readonly IAdminUnitOfWork _unitOfWork;
    public AdminSubjectService(IAdminSubjectRepository repository, IAdminUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<(List<Subject> Subjects, PaginationViewModel Pagination)> GetIndexAsync(int page)
    {
        var pagination = new PaginationViewModel(await _repository.CountAsync(), page);
        return (await _repository.GetPageAsync(pagination.Offset, PaginationViewModel.PageSize), pagination);
    }
    public Task<Subject?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);
    public async Task<SubjectViewModel?> GetEditAsync(int id)
    {
        var subject = await _repository.GetByIdAsync(id);
        return subject == null ? null : new SubjectViewModel
        {
            Id = subject.Id, Name = subject.Name, Description = subject.Description, Status = subject.Status
        };
    }
    public async Task<AdminResult> CreateAsync(SubjectViewModel model)
    {
        var name = model.Name.Trim();
        if (await _repository.NameExistsAsync(name))
            return AdminResult.Invalid("Tên môn học đã tồn tại.", nameof(model.Name));
        var now = DateTime.UtcNow;
        _repository.Add(new Subject
        {
            Name = name, Description = model.Description?.Trim(), Status = model.Status,
            CreatedAt = now, UpdatedAt = now
        });
        try { await _unitOfWork.SaveChangesAsync(); }
        catch (DbUpdateException) { return AdminResult.Invalid("Không thể lưu môn học. Vui lòng kiểm tra dữ liệu."); }
        return AdminResult.Ok("Thêm môn học thành công.");
    }
    public async Task<AdminResult> EditAsync(int id, SubjectViewModel model)
    {
        var name = model.Name.Trim();
        if (await _repository.NameExistsAsync(name, id))
            return AdminResult.Invalid("Tên môn học đã được sử dụng.", nameof(model.Name));
        var subject = await _repository.GetByIdAsync(id, tracking: true);
        if (subject == null) return AdminResult.Missing();
        subject.Name = name;
        subject.Description = model.Description?.Trim();
        subject.Status = model.Status;
        subject.UpdatedAt = DateTime.UtcNow;
        try { await _unitOfWork.SaveChangesAsync(); }
        catch (DbUpdateException) { return AdminResult.Invalid("Không thể cập nhật môn học. Vui lòng thử lại."); }
        return AdminResult.Ok("Cập nhật môn học thành công.");
    }
    public async Task<AdminResult> DeactivateAsync(int id)
    {
        var subject = await _repository.GetByIdAsync(id, tracking: true);
        if (subject == null) return AdminResult.Missing();
        if (subject.Status == SubjectStatus.Inactive) return AdminResult.Invalid("Môn học này đã ngừng hoạt động.");
        subject.Status = SubjectStatus.Inactive;
        subject.UpdatedAt = DateTime.UtcNow;
        try { await _unitOfWork.SaveChangesAsync(); }
        catch (DbUpdateException) { return AdminResult.Invalid("Không thể ngừng kích hoạt môn học. Vui lòng thử lại."); }
        return AdminResult.Ok("Đã ngừng kích hoạt môn học thành công.");
    }
}
