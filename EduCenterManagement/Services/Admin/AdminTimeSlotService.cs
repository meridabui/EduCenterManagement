using System.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Services.Admin;

public class AdminTimeSlotService : IAdminTimeSlotService
{
    private readonly IAdminTimeSlotRepository _repository;
    private readonly IAdminUnitOfWork _unitOfWork;
    public AdminTimeSlotService(IAdminTimeSlotRepository repository, IAdminUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }
    public Task<List<TimeSlot>> GetAllAsync() => _repository.GetAllAsync();
    public Task<TimeSlot?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);
    public async Task<TimeSlotViewModel?> GetEditAsync(int id)
    {
        var slot = await _repository.GetByIdAsync(id);
        return slot == null ? null : new TimeSlotViewModel
        {
            Id = slot.Id, Name = slot.Name, StartTime = slot.StartTime, EndTime = slot.EndTime,
            UsedByClasses = await _repository.GetUsageAsync(id)
        };
    }
    public async Task<AdminResult> CreateAsync(TimeSlotViewModel model, bool inputValid)
    {
        if (model.StartTime >= model.EndTime)
            return AdminResult.Invalid("Giờ kết thúc phải sau giờ bắt đầu.", nameof(model.EndTime));
        if (!inputValid) return new() { Status = AdminResultStatus.Invalid };
        var name = model.Name.Trim();
        if (await _repository.NameExistsAsync(name))
            return AdminResult.Invalid("Tên khung giờ đã tồn tại.", nameof(model.Name));
        if (await _repository.OverlapsAsync(model.StartTime, model.EndTime))
            return AdminResult.Invalid("Khung giờ mới bị trùng hoặc chồng lấn với khung giờ hiện có.");
        var now = DateTime.UtcNow;
        _repository.Add(new TimeSlot
        {
            Name = name, StartTime = model.StartTime, EndTime = model.EndTime, CreatedAt = now, UpdatedAt = now
        });
        try { await _unitOfWork.SaveChangesAsync(); }
        catch (DbUpdateException) { return AdminResult.Invalid("Không thể lưu khung giờ. Vui lòng kiểm tra dữ liệu."); }
        return AdminResult.Ok("Thêm khung giờ thành công.");
    }
    public async Task<AdminResult> EditAsync(int id, TimeSlotViewModel model, bool inputValid, bool inputValidWithoutTimes)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await _repository.LockAsync(id) == 0) return AdminResult.Missing();
        var slot = (await _repository.GetByIdAsync(id, tracking: true))!;
        model.UsedByClasses = await _repository.GetUsageAsync(model.Id);
        var timeChanged = model.StartTime != slot.StartTime || model.EndTime != slot.EndTime;
        var result = new AdminResult();
        if (model.IsTimeLocked)
        {
            if (timeChanged)
                result = AdminResult.Invalid("Không thể thay đổi giờ vì khung giờ đã được lớp sử dụng. Bạn vẫn có thể sửa tên hoặc tạo khung giờ mới.");
            model.StartTime = slot.StartTime;
            model.EndTime = slot.EndTime;
            inputValid = inputValidWithoutTimes;
        }
        if (model.StartTime >= model.EndTime)
        {
            result.Status = AdminResultStatus.Invalid;
            result.Errors.Add(new(nameof(model.EndTime), "Giờ kết thúc phải sau giờ bắt đầu."));
        }
        if (!inputValid || !result.Success)
        {
            result.Status = AdminResultStatus.Invalid;
            return result;
        }
        var name = model.Name.Trim();
        if (await _repository.NameExistsAsync(name, id))
            return AdminResult.Invalid("Tên khung giờ đã được sử dụng.", nameof(model.Name));
        if (timeChanged && !model.IsTimeLocked && await _repository.OverlapsAsync(model.StartTime, model.EndTime, id))
            return AdminResult.Invalid("Khung giờ bị trùng hoặc chồng lấn với khung giờ khác.");
        slot.Name = name;
        slot.StartTime = model.StartTime;
        slot.EndTime = model.EndTime;
        slot.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException) { return AdminResult.Invalid("Không thể cập nhật khung giờ. Vui lòng thử lại."); }
        return AdminResult.Ok("Cập nhật khung giờ thành công.");
    }
    public async Task<AdminResult> DeactivateAsync(int id)
    {
        var slot = await _repository.GetByIdAsync(id, tracking: true);
        if (slot == null) return AdminResult.Missing();
        if (slot.Status == TimeSlotStatus.Inactive) return AdminResult.Invalid("Khung giờ này đã ngừng hoạt động.");
        slot.Status = TimeSlotStatus.Inactive;
        slot.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
        return AdminResult.Ok("Đã ngừng kích hoạt khung giờ thành công.");
    }
    public async Task<AdminResult> ReactivateAsync(int id)
    {
        var slot = await _repository.GetByIdAsync(id, tracking: true);
        if (slot == null) return AdminResult.Missing();
        if (slot.Status == TimeSlotStatus.Active) return AdminResult.Invalid("Khung giờ này đang hoạt động.");
        if (await _repository.OverlapsAsync(slot.StartTime, slot.EndTime, id, activeOnly: true))
            return AdminResult.Invalid("Không thể kích hoạt lại vì khung giờ bị chồng lấn với một khung giờ đang hoạt động.");
        slot.Status = TimeSlotStatus.Active;
        slot.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
        return AdminResult.Ok("Kích hoạt lại khung giờ thành công.");
    }
    public async Task<AdminResult> GenerateDefaultSlotsAsync()
    {
        var defaultSlots = new List<(string Name, TimeOnly StartTime, TimeOnly EndTime)>
        {
            ("Slot 1", new(7, 0), new(9, 0)), ("Slot 2", new(9, 0), new(11, 0)),
            ("Slot 3", new(13, 0), new(15, 0)), ("Slot 4", new(15, 0), new(17, 0)),
            ("Slot 5", new(17, 0), new(19, 0)), ("Slot 6", new(19, 0), new(21, 0))
        };
        var existingSlots = await _repository.GetAllAsync(orderByTime: false, tracking: true);
        var now = DateTime.UtcNow;
        var addedSlots = new List<TimeSlot>();
        var skippedNames = new List<string>();
        foreach (var slot in defaultSlots)
        {
            bool nameExists = existingSlots.Any(e => string.Equals(e.Name, slot.Name, StringComparison.OrdinalIgnoreCase));
            bool overlaps = existingSlots.Any(e => e.Status == TimeSlotStatus.Active
                && slot.StartTime < e.EndTime && slot.EndTime > e.StartTime);
            if (nameExists || overlaps) { skippedNames.Add(slot.Name); continue; }
            var newSlot = new TimeSlot
            {
                Name = slot.Name, StartTime = slot.StartTime, EndTime = slot.EndTime,
                Status = TimeSlotStatus.Active, CreatedAt = now, UpdatedAt = now
            };
            addedSlots.Add(newSlot);
            existingSlots.Add(newSlot);
        }
        if (addedSlots.Count == 0)
            return AdminResult.Invalid("Tất cả 6 khung giờ mặc định (Slot 1 - Slot 6) đã tồn tại hoặc bị trùng lặp thời gian.");
        _repository.AddRange(addedSlots);
        await _unitOfWork.SaveChangesAsync();
        return AdminResult.Ok(skippedNames.Count > 0
            ? $"Đã khởi tạo thành công {addedSlots.Count} khung giờ mặc định. (Bỏ qua: {string.Join(", ", skippedNames)} do đã tồn tại hoặc trùng lấn)."
            : "Đã tự động khởi tạo thành công tất cả 6 khung giờ mặc định (Slot 1 đến Slot 6).");
    }
}
