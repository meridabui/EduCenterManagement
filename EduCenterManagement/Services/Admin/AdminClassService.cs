using System.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Services.Admin;

public class AdminClassService : IAdminClassService
{
    private readonly IAdminClassRepository _repository;
    private readonly IAdminUnitOfWork _unitOfWork;
    private readonly IClassSessionService _sessions;
    private readonly IClassLifecycleService _lifecycle;

    public AdminClassService(IAdminClassRepository repository, IAdminUnitOfWork unitOfWork,
        IClassSessionService sessions, IClassLifecycleService lifecycle)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _sessions = sessions;
        _lifecycle = lifecycle;
    }

    public async Task<AdminResult<AdminClassIndexData>> GetIndexAsync(int? subjectId, int? teacherId, string? status, int page)
    {
        if (!string.IsNullOrEmpty(status) && status != "Open,InProgress"
            && (!Enum.TryParse<ClassStatus>(status, out var parsedStatus) || !Enum.IsDefined(parsedStatus)))
            return new() { Status = AdminResultStatus.BadRequest, Message = "Trạng thái lớp học không hợp lệ." };
        var form = await LoadFormDataAsync();
        var filterSubjects = new SelectList(await _repository.GetSubjectsAsync(), nameof(Subject.Id), nameof(Subject.Name), subjectId);
        var filterTeachers = new SelectList(await _repository.GetTeachersAsync(subjectId), nameof(TeacherProfile.Id), nameof(TeacherProfile.FullName), teacherId);
        var statuses = new List<SelectListItem>
        {
            new("Bản nháp", "Draft", status == "Draft"), new("Mở đăng ký", "Open", status == "Open"),
            new("Đang học", "InProgress", status == "InProgress"), new("Hoàn thành", "Completed", status == "Completed"),
            new("Đã hủy", "Cancelled", status == "Cancelled"),
            new("Mở đăng ký và đang học", "Open,InProgress", status == "Open,InProgress")
        };
        var pagination = new PaginationViewModel(await _repository.CountAsync(subjectId, teacherId, status), page);
        return new()
        {
            Data = new()
            {
                Form = form, FilterSubjects = filterSubjects, FilterTeachers = filterTeachers, FilterStatuses = statuses,
                HasClassFilters = subjectId.HasValue || teacherId.HasValue || !string.IsNullOrEmpty(status),
                Pagination = pagination,
                Classes = await _repository.GetPageAsync(subjectId, teacherId, status, pagination.Offset, PaginationViewModel.PageSize)
            }
        };
    }

    public async Task<AdminResult> CreateAsync(ClassViewModel model)
    {
        var validation = await ValidateClassAsync(model);
        if (!validation.Success) return validation;
        var slotIds = model.Schedules.Select(s => s.TimeSlotId).Distinct().ToList();
        var now = DateTime.UtcNow;
        var classItem = new Class
        {
            Name = model.Name, SubjectId = model.SubjectId, TeacherId = model.TeacherId,
            StartDate = model.StartDate, EndDate = model.EndDate, MaxStudents = model.MaxStudents, Fee = model.Fee,
            Status = ClassStatus.Draft, CreatedAt = now, UpdatedAt = now
        };
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            _repository.AddClass(classItem);
            await _unitOfWork.SaveChangesAsync();
            var schedules = model.Schedules.Select(s => new ClassSchedule
            {
                ClassId = classItem.Id, DayOfWeek = s.DayOfWeek, TimeSlotId = s.TimeSlotId, CreatedAt = now, UpdatedAt = now
            }).ToList();
            _repository.AddSchedules(schedules);
            await _unitOfWork.SaveChangesAsync();
            var slots = await _repository.GetSlotsAsync(slotIds);
            foreach (var schedule in schedules) schedule.TimeSlot = slots[schedule.TimeSlotId];
            var sessionResult = await _sessions.SynchronizeAsync(classItem, schedules);
            await transaction.CommitAsync();
            var message = $"Đã tạo lớp {classItem.Name} với {schedules.Count} lịch học và {sessionResult.Added} buổi học.";
            message += " Lớp đang ở Bản nháp; hãy mở đăng ký khi đã kiểm tra thông tin.";
            if (!model.TeacherId.HasValue) message += " Vui lòng phân công giáo viên sau.";
            return AdminResult.Ok(message);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return AdminResult.Invalid("Không thể lưu lớp và lịch học. Vui lòng kiểm tra dữ liệu.");
        }
    }

    public async Task<AdminResult<object>> CheckConflictAsync(int teacherId, int? classId,
        DateOnly? startDate, DateOnly? endDate, int? timeSlotId, int[] days, int[] timeSlotIds)
    {
        if (teacherId <= 0) return JsonResult(new { ok = true });
        List<(int Day, int SlotId)> proposed;
        DateOnly start, end;
        if (classId.HasValue && !timeSlotId.HasValue && (days == null || days.Length == 0)
            && (timeSlotIds == null || timeSlotIds.Length == 0))
        {
            var classItem = await _repository.GetByIdAsync(classId.Value);
            if (classItem == null) return new() { Status = AdminResultStatus.NotFound };
            start = classItem.StartDate;
            end = classItem.EndDate;
            var withSchedules = await _repository.GetWithSchedulesAsync(classId.Value);
            proposed = withSchedules?.ClassSchedules.Select(s => (s.DayOfWeek, s.TimeSlotId)).ToList() ?? new();
        }
        else
        {
            if (!startDate.HasValue || !endDate.HasValue || endDate <= startDate)
                return JsonResult(new { ok = false, message = "Khoảng ngày học không hợp lệ." }, AdminResultStatus.BadRequest);
            start = startDate.Value;
            end = endDate.Value;
            if (days == null || days.Length == 0) return JsonResult(new { ok = true, incomplete = true });
            if (timeSlotIds != null && timeSlotIds.Length > 0)
            {
                if (days.Length != timeSlotIds.Length)
                    return JsonResult(new { ok = false, message = "Số thứ và số ca học không khớp." }, AdminResultStatus.BadRequest);
                proposed = days.Zip(timeSlotIds, (day, slot) => (day, slot)).Distinct().ToList();
            }
            else if (timeSlotId.HasValue) proposed = days.Distinct().Select(day => (day, timeSlotId.Value)).ToList();
            else return JsonResult(new { ok = true, incomplete = true });
        }
        var conflicts = await FindConflictsAsync(teacherId, classId, start, end, proposed);
        if (conflicts.Count > 0) return JsonResult(new { ok = false, conflicts }, AdminResultStatus.Conflict);
        return JsonResult(new { ok = true });
    }

    public async Task<AdminResult<object>> GetAssignInfoAsync(int id)
    {
        var classItem = await _repository.GetWithSchedulesAsync(id, includeSlots: true);
        if (classItem == null) return new() { Status = AdminResultStatus.NotFound };
        if (classItem.Status != ClassStatus.Draft && classItem.Status != ClassStatus.Open)
            return JsonResult(new { message = "Chỉ được phân công giáo viên cho lớp Bản nháp hoặc Mở đăng ký." }, AdminResultStatus.BadRequest);
        var teachers = await GetTeachersBySubjectAsync(classItem.SubjectId);
        var schedules = classItem.ClassSchedules.OrderBy(s => s.DayOfWeek).Select(s => new
        {
            dayOfWeek = s.DayOfWeek, dayLabel = DayLabel(s.DayOfWeek), timeSlotId = s.TimeSlotId,
            slotName = s.TimeSlot.Name, startTime = s.TimeSlot.StartTime.ToString("HH:mm"), endTime = s.TimeSlot.EndTime.ToString("HH:mm")
        }).ToList();
        return JsonResult(new
        {
            classId = classItem.Id, className = classItem.Name, currentTeacherId = classItem.TeacherId,
            startDate = classItem.StartDate.ToString("yyyy-MM-dd"), endDate = classItem.EndDate.ToString("yyyy-MM-dd"), schedules, teachers
        });
    }

    public async Task<AdminResult<object>> AssignTeacherAsync(AssignTeacherViewModel model)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await _repository.LockForAssignmentAsync(model.ClassId) == 0)
            return JsonResult(new { message = "Lớp không tồn tại hoặc không còn ở trạng thái Bản nháp / Mở đăng ký. Vui lòng tải lại trang." }, AdminResultStatus.BadRequest);
        var classItem = await _repository.GetWithSchedulesAsync(model.ClassId, tracking: true);
        if (classItem == null) return new() { Status = AdminResultStatus.NotFound };
        if (!await _repository.IsActiveTeacherForSubjectAsync(model.TeacherId, classItem.SubjectId))
            return JsonResult(new { message = "Giáo viên không hoạt động hoặc không thuộc môn học của lớp." }, AdminResultStatus.BadRequest);
        var proposed = classItem.ClassSchedules.Select(s => (s.DayOfWeek, s.TimeSlotId)).ToList();
        var conflicts = await FindConflictsAsync(model.TeacherId, classItem.Id, classItem.StartDate, classItem.EndDate, proposed);
        if (conflicts.Count > 0) return JsonResult(new { ok = false, conflicts }, AdminResultStatus.Conflict);
        classItem.TeacherId = model.TeacherId;
        classItem.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return JsonResult(new { ok = true, message = $"Đã gán giáo viên cho lớp {classItem.Name}." });
    }

    public async Task<AdminResult<object>> GetCapacityAsync(int id)
    {
        var classItem = await _repository.GetByIdAsync(id);
        if (classItem == null) return new() { Status = AdminResultStatus.NotFound };
        var now = DateTime.UtcNow;
        var enrollments = (await _repository.GetCapacityStudentsAsync(id, now)).Select(e => new
        {
            studentName = e.StudentName, parentId = e.ParentId, reservedUntil = e.ReservedUntil, status = e.Status
        }).ToList();
        int confirmed = enrollments.Count(e => e.status == "Confirmed");
        int reserved = enrollments.Count(e => e.status == "Reserved");
        int occupied = confirmed + reserved;
        return JsonResult(new
        {
            classId = classItem.Id, className = classItem.Name, max = classItem.MaxStudents, confirmed, reserved, occupied,
            available = Math.Max(classItem.MaxStudents - occupied, 0), isFull = occupied >= classItem.MaxStudents, students = enrollments
        });
    }

    public async Task<AdminClassDetailsData?> GetDetailsAsync(int id)
    {
        var classItem = await _repository.GetDetailsAsync(id);
        if (classItem == null) return null;
        return new()
        {
            Class = classItem, SessionCount = await _repository.CountSessionsAsync(classItem.Id),
            HasPaidEnrollments = await _repository.HasConfirmedEnrollmentsAsync(classItem.Id) || await _repository.HasPaidPaymentsAsync(classItem.Id)
        };
    }

    public async Task<AdminResult> GenerateSessionsAsync(int id)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var classItem = await _repository.GetWithSchedulesAsync(id, tracking: true, includeSlots: true);
        if (classItem == null) return AdminResult.Missing();
        if (classItem.Status != ClassStatus.Draft) return AdminResult.Invalid("Chỉ được tạo buổi học khi lớp còn Bản nháp.");
        if (!classItem.ClassSchedules.Any() || classItem.StartDate > classItem.EndDate)
            return AdminResult.Invalid("Vui lòng thiết lập lịch học và khoảng ngày hợp lệ trước khi tạo buổi.");
        try
        {
            var result = await _sessions.SynchronizeAsync(classItem, classItem.ClassSchedules.ToList(), onlyIfEmpty: true);
            await transaction.CommitAsync();
            return AdminResult.Ok(result.Added > 0 ? $"Đã tạo {result.Added} buổi học theo lịch của lớp."
                : result.Total > 0 ? "Lớp đã có buổi học; không tạo trùng." : "Không có ngày học phù hợp trong khoảng thời gian của lớp.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return AdminResult.Invalid("Không thể tạo buổi học. Vui lòng thử lại.");
        }
    }

    public async Task<AdminResult<AdminClassEditData>> GetEditAsync(int id)
    {
        var classItem = await _repository.GetWithSchedulesAsync(id);
        if (classItem == null) return new() { Status = AdminResultStatus.NotFound };
        var model = new ClassViewModel
        {
            Id = classItem.Id, SubjectId = classItem.SubjectId, TeacherId = classItem.TeacherId, Name = classItem.Name,
            StartDate = classItem.StartDate, EndDate = classItem.EndDate, MaxStudents = classItem.MaxStudents,
            Fee = classItem.Fee, Status = classItem.Status,
            Schedules = classItem.ClassSchedules.OrderBy(s => s.DayOfWeek).Select(s => new ClassScheduleInputViewModel
            {
                DayOfWeek = s.DayOfWeek, TimeSlotId = s.TimeSlotId
            }).ToList()
        };
        if (classItem.Status != ClassStatus.Draft)
            return new() { Status = AdminResultStatus.Invalid, Message = "Thông tin lớp đã khóa. Chỉ được sửa lớp ở trạng thái Bản nháp." };
        return new() { Data = new() { Model = model, Form = await LoadFormDataAsync(model.SubjectId, model.TeacherId) } };
    }

    public async Task<AdminResult<AdminClassEditData>> EditAsync(int id, ClassViewModel model, bool inputValid)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await _repository.LockDraftAsync(id) == 0)
            return new() { Status = AdminResultStatus.Conflict, Message = "Thông tin lớp đã khóa. Chỉ được sửa lớp ở trạng thái Bản nháp." };
        if (model.Status != ClassStatus.Draft)
            return new() { Status = AdminResultStatus.BadRequest, Message = "Hãy dùng thao tác chuyển trạng thái riêng trong Chi tiết lớp học." };
        var classItem = await _repository.GetWithSchedulesAsync(id, tracking: true);
        if (classItem == null) return new() { Status = AdminResultStatus.NotFound };
        model.Schedules ??= new();
        model.Name = (model.Name ?? string.Empty).Trim();
        var data = new AdminClassEditData { Model = model, Form = await LoadFormDataAsync(model.SubjectId, model.TeacherId) };
        AdminResult<AdminClassEditData> Back(AdminResult result) => new()
        {
            Status = result.Status, Message = result.Message, Errors = result.Errors, Data = data
        };
        if (!inputValid) return Back(new() { Status = AdminResultStatus.Invalid });
        var validation = await ValidateClassAsync(model, classItem);
        if (!validation.Success) return Back(validation);
        var slotIds = model.Schedules.Select(s => s.TimeSlotId).Distinct().ToList();
        var now = DateTime.UtcNow;
        try
        {
            classItem.Name = model.Name;
            classItem.SubjectId = model.SubjectId;
            classItem.TeacherId = model.TeacherId;
            classItem.StartDate = model.StartDate;
            classItem.EndDate = model.EndDate;
            classItem.MaxStudents = model.MaxStudents;
            classItem.Fee = model.Fee;
            classItem.Status = ClassStatus.Draft;
            classItem.UpdatedAt = now;
            var oldSchedules = classItem.ClassSchedules.ToList();
            var slots = await _repository.GetSlotsAsync(slotIds);
            var newSchedules = model.Schedules.Select(s => oldSchedules.FirstOrDefault(old => old.DayOfWeek == s.DayOfWeek
                && old.TimeSlotId == s.TimeSlotId) ?? new ClassSchedule
            {
                ClassId = classItem.Id, DayOfWeek = s.DayOfWeek, TimeSlotId = s.TimeSlotId, CreatedAt = now, UpdatedAt = now
            }).ToList();
            foreach (var schedule in newSchedules)
            {
                schedule.TimeSlot = slots[schedule.TimeSlotId];
                if (schedule.Id == 0) _repository.AddSchedule(schedule);
            }
            await _unitOfWork.SaveChangesAsync();
            await _sessions.SynchronizeAsync(classItem, newSchedules);
            _repository.RemoveSchedules(oldSchedules.Except(newSchedules));
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
            return new() { Message = "Đã cập nhật thành công." };
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Back(AdminResult.Invalid("Không thể cập nhật lớp và lịch học."));
        }
    }

    public async Task<AdminResult<Class>> GetDeleteAsync(int id)
    {
        var classItem = await _repository.GetDeleteAsync(id);
        if (classItem == null) return new() { Status = AdminResultStatus.NotFound };
        if (classItem.Status != ClassStatus.Draft && classItem.Status != ClassStatus.Open)
            return new() { Status = AdminResultStatus.Invalid, Message = "Chỉ được hủy lớp Bản nháp hoặc Mở đăng ký." };
        if (await _repository.HasConfirmedEnrollmentsAsync(id) || await _repository.HasPaidPaymentsAsync(id))
            return new() { Status = AdminResultStatus.Invalid, Message = "Không thể hủy lớp có đăng ký đã thanh toán." };
        return new() { Data = classItem };
    }

    public async Task<ClassTransitionResult> ChangeStatusAsync(int id, ClassStatus targetStatus)
    {
        try { return await _lifecycle.ChangeAsync(id, targetStatus); }
        catch (Exception ex) when (ex is DbUpdateException or System.Data.Common.DbException)
        {
            return new(false, "Không thể chuyển trạng thái. Vui lòng tải lại trang và thử lại.");
        }
    }

    public async Task<object> GetTeachersBySubjectAsync(int subjectId)
    {
        if (subjectId <= 0) return Array.Empty<object>();
        return (await _repository.GetTeachersAsync(subjectId, activeOnly: true))
            .Select(t => new { id = t.Id, fullName = t.FullName }).ToList();
    }

    private async Task<AdminClassFormData> LoadFormDataAsync(int? selectedSubjectId = null, int? selectedTeacherId = null)
    {
        var subjects = await _repository.GetSubjectsAsync(activeOnly: true);
        var teachers = await _repository.GetTeachersAsync(selectedSubjectId, activeOnly: true);
        var slots = await _repository.GetActiveSlotsAsync();
        return new()
        {
            Subjects = new SelectList(subjects, "Id", "Name", selectedSubjectId),
            Teachers = new SelectList(teachers, "Id", "FullName", selectedTeacherId),
            TimeSlots = slots.Select(t => new SelectListItem
            {
                Value = t.Id.ToString(), Text = $"{t.Name} ({t.StartTime:HH\\:mm} - {t.EndTime:HH\\:mm})"
            }).ToList()
        };
    }

    private async Task<AdminResult> ValidateClassAsync(ClassViewModel model, Class? currentClass = null)
    {
        int classId = currentClass == null ? 0 : currentClass.Id;
        if (await _repository.NameExistsAsync(model.Name, classId))
            return AdminResult.Invalid("Tên lớp đã tồn tại.", nameof(model.Name));
        bool subjectActive = await _repository.IsSubjectActiveAsync(model.SubjectId);
        bool sameSubject = currentClass != null && currentClass.SubjectId == model.SubjectId;
        if (!subjectActive && !sameSubject)
            return AdminResult.Invalid("Môn học không tồn tại hoặc đã ngừng hoạt động.", nameof(model.SubjectId));
        var slotIds = model.Schedules.Select(s => s.TimeSlotId).Distinct().ToList();
        if (await _repository.CountActiveSlotsAsync(slotIds) != slotIds.Count)
            return AdminResult.Invalid("Có ca học không tồn tại hoặc đã ngừng hoạt động.", nameof(model.Schedules));
        if (!model.TeacherId.HasValue) return AdminResult.Ok();
        if (!await _repository.IsActiveTeacherForSubjectAsync(model.TeacherId.Value, model.SubjectId))
            return AdminResult.Invalid("Giáo viên không hoạt động hoặc không thuộc môn học.", nameof(model.TeacherId));
        var schedules = model.Schedules.Select(s => (s.DayOfWeek, s.TimeSlotId)).ToList();
        var conflicts = await FindConflictsAsync(model.TeacherId.Value, classId, model.StartDate, model.EndDate, schedules);
        if (conflicts.Count > 0)
            return AdminResult.Invalid("Giáo viên bị trùng lịch: " + DescribeConflicts(conflicts), nameof(model.TeacherId));
        return AdminResult.Ok();
    }

    private async Task<List<ClassConflictViewModel>> FindConflictsAsync(int teacherId, int? currentClassId,
        DateOnly start, DateOnly end, List<(int Day, int SlotId)> proposed)
    {
        var result = new List<ClassConflictViewModel>();
        if (proposed.Count == 0) return result;
        var slotIds = proposed.Select(p => p.SlotId).Distinct().ToList();
        var slots = await _repository.GetSlotsAsync(slotIds, tracking: false);
        foreach (var group in proposed.GroupBy(p => p.SlotId))
        {
            if (!slots.TryGetValue(group.Key, out var proposedSlot)) continue;
            var days = group.Select(g => g.Day).Distinct().ToList();
            var found = await _repository.FindConflictsAsync(teacherId, currentClassId, start, end, days, proposedSlot.StartTime, proposedSlot.EndTime);
            result.AddRange(found.Select(f => new ClassConflictViewModel
            {
                ClassName = f.ClassName, DayOfWeek = f.DayOfWeek, DayLabel = DayLabel(f.DayOfWeek), SlotName = f.SlotName
            }));
        }
        return result.GroupBy(c => (c.ClassName, c.DayOfWeek, c.SlotName)).Select(g => g.First()).OrderBy(c => c.DayOfWeek).ToList();
    }

    private static string DayLabel(int day) => day == 8 ? "CN" : $"Thứ {day}";
    private static string DescribeConflicts(IEnumerable<ClassConflictViewModel> conflicts)
        => string.Join("; ", conflicts.Select(c => $"lớp {c.ClassName} ({c.DayLabel}, {c.SlotName})"));
    private static AdminResult<object> JsonResult(object data, AdminResultStatus status = AdminResultStatus.Success)
        => new() { Data = data, Status = status };
}
