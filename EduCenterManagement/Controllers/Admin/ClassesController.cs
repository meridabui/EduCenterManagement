using System;
using System.Collections.Generic;
using System.Linq;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class ClassesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClassesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // DANH SÁCH LỚP
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var classes = await BuildIndexAsync();

            return View("~/Views/Admin/Classes/Index.cshtml", classes);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // TẠO LỚP + NHIỀU LỊCH HỌC
        // Mỗi lịch gồm DayOfWeek và TimeSlotId riêng.
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClassViewModel model)
        {
            model.Name = (model.Name ?? string.Empty).Trim();

            if (model.Schedules == null)
                model.Schedules = new List<ClassScheduleInputViewModel>();

            ValidateSchedules(model);

            if (model.EndDate < model.StartDate)
            {
                ModelState.AddModelError(
                    nameof(model.EndDate),
                    "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
            }

            if (!ModelState.IsValid)
                return await ReturnCreateErrorAsync();

            bool nameExists = await _context.Classes.AnyAsync(c =>
                c.Name == model.Name);

            if (nameExists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Tên lớp đã tồn tại.");

                return await ReturnCreateErrorAsync();
            }

            bool subjectExists = await _context.Subjects.AnyAsync(s =>
                s.Id == model.SubjectId &&
                s.Status == SubjectStatus.Active);

            if (!subjectExists)
            {
                ModelState.AddModelError(
                    nameof(model.SubjectId),
                    "Môn học không tồn tại hoặc đã ngừng hoạt động.");

                return await ReturnCreateErrorAsync();
            }

            var slotIds = model.Schedules
                .Select(s => s.TimeSlotId)
                .Distinct()
                .ToList();

            var activeSlotIds = await _context.TimeSlots
                .Where(t =>
                    slotIds.Contains(t.Id) &&
                    t.Status == TimeSlotStatus.Active)
                .Select(t => t.Id)
                .ToListAsync();

            if (activeSlotIds.Count != slotIds.Count)
            {
                ModelState.AddModelError(
                    nameof(model.Schedules),
                    "Có ca học không tồn tại hoặc đã ngừng hoạt động.");

                return await ReturnCreateErrorAsync();
            }

            if (model.TeacherId.HasValue)
            {
                bool teacherValid =
                    await IsActiveTeacherForSubjectAsync(
                        model.TeacherId.Value,
                        model.SubjectId);

                if (!teacherValid)
                {
                    ModelState.AddModelError(
                        nameof(model.TeacherId),
                        "Giáo viên không hoạt động hoặc không thuộc môn học.");

                    return await ReturnCreateErrorAsync();
                }

                var proposed = model.Schedules
                    .Select(s => (s.DayOfWeek, s.TimeSlotId))
                    .ToList();

                var conflicts = await FindConflictsAsync(
                    model.TeacherId.Value,
                    null,
                    model.StartDate,
                    model.EndDate,
                    proposed);

                if (conflicts.Count > 0)
                {
                    ModelState.AddModelError(
                        nameof(model.TeacherId),
                        "Giáo viên bị trùng lịch: " +
                        DescribeConflicts(conflicts));

                    return await ReturnCreateErrorAsync();
                }
            }

            var now = DateTime.UtcNow;

            var classItem = new Class
            {
                Name = model.Name,
                SubjectId = model.SubjectId,
                TeacherId = model.TeacherId,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                MaxStudents = model.MaxStudents,
                Fee = model.Fee,
                Status = model.TeacherId.HasValue
                    ? ClassStatus.Open
                    : ClassStatus.Draft,
                CreatedAt = now,
                UpdatedAt = now
            };

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Classes.Add(classItem);
                await _context.SaveChangesAsync();

                var schedules = model.Schedules.Select(s =>
                    new ClassSchedule
                    {
                        ClassId = classItem.Id,
                        DayOfWeek = s.DayOfWeek,
                        TimeSlotId = s.TimeSlotId,
                        CreatedAt = now,
                        UpdatedAt = now
                    }).ToList();

                _context.ClassSchedules.AddRange(schedules);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Đã tạo lớp {classItem.Name} với " +
                    $"{schedules.Count} lịch học.";

                if (!model.TeacherId.HasValue)
                {
                    TempData["SuccessMessage"] +=
                        " Vui lòng phân công giáo viên sau.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Không thể lưu lớp và lịch học. " +
                    "Vui lòng kiểm tra dữ liệu.";

                return RedirectToAction(nameof(Index));
            }
        }

        // =========================================================
        // API KIỂM TRA TRÙNG LỊCH
        //
        // Khi tạo lớp:
        // teacherId, startDate, endDate,
        // days=2&timeSlotIds=1&days=4&timeSlotIds=3
        //
        // Có thể dùng timeSlotId chung để tương thích
        // với giao diện cũ.
        //
        // Khi phân công:
        // teacherId + classId
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> CheckConflict(
            int teacherId,
            int? classId,
            DateOnly? startDate,
            DateOnly? endDate,
            int? timeSlotId,
            [FromQuery] int[] days,
            [FromQuery] int[] timeSlotIds)
        {
            if (teacherId <= 0)
                return Ok(new { ok = true });

            List<(int Day, int SlotId)> proposed;
            DateOnly start;
            DateOnly end;

            if (classId.HasValue &&
                !timeSlotId.HasValue &&
                (days == null || days.Length == 0) &&
                (timeSlotIds == null || timeSlotIds.Length == 0))
            {
                var classItem = await _context.Classes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == classId.Value);

                if (classItem == null)
                    return NotFound();

                start = classItem.StartDate;
                end = classItem.EndDate;

                proposed = await _context.ClassSchedules
                    .AsNoTracking()
                    .Where(s => s.ClassId == classId.Value)
                    .Select(s => new
                    {
                        s.DayOfWeek,
                        s.TimeSlotId
                    })
                    .Select(s => new ValueTuple<int, int>(
                        s.DayOfWeek,
                        s.TimeSlotId))
                    .ToListAsync();
            }
            else
            {
                if (!startDate.HasValue ||
                    !endDate.HasValue ||
                    endDate.Value < startDate.Value)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "Khoảng ngày học không hợp lệ."
                    });
                }

                start = startDate.Value;
                end = endDate.Value;

                if (days == null || days.Length == 0)
                    return Ok(new { ok = true, incomplete = true });

                if (timeSlotIds != null && timeSlotIds.Length > 0)
                {
                    if (days.Length != timeSlotIds.Length)
                    {
                        return BadRequest(new
                        {
                            ok = false,
                            message = "Số thứ và số ca học không khớp."
                        });
                    }

                    proposed = days
                        .Zip(timeSlotIds, (day, slot) => (day, slot))
                        .Distinct()
                        .ToList();
                }
                else if (timeSlotId.HasValue)
                {
                    proposed = days
                        .Distinct()
                        .Select(day => (day, timeSlotId.Value))
                        .ToList();
                }
                else
                {
                    return Ok(new { ok = true, incomplete = true });
                }
            }

            var conflicts = await FindConflictsAsync(
                teacherId,
                classId,
                start,
                end,
                proposed);

            if (conflicts.Count > 0)
            {
                return StatusCode(
                    StatusCodes.Status409Conflict,
                    new
                    {
                        ok = false,
                        conflicts
                    });
            }

            return Ok(new { ok = true });
        }

        // =========================================================
        // THÔNG TIN PHÂN CÔNG GIÁO VIÊN NHANH
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> AssignInfo(int id)
        {
            var classItem = await _context.Classes
                .AsNoTracking()
                .Include(c => c.ClassSchedules)
                    .ThenInclude(s => s.TimeSlot)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (classItem == null)
                return NotFound();

            var teachers = await _context.TeacherProfiles
                .AsNoTracking()
                .Where(t =>
                    t.SubjectId == classItem.SubjectId &&
                    t.User.Status == UserStatus.Active &&
                    t.User.Role == UserRole.Teacher)
                .OrderBy(t => t.FullName)
                .Select(t => new
                {
                    id = t.Id,
                    fullName = t.FullName
                })
                .ToListAsync();

            var schedules = classItem.ClassSchedules
                .OrderBy(s => s.DayOfWeek)
                .Select(s => new
                {
                    dayOfWeek = s.DayOfWeek,
                    dayLabel = DayLabel(s.DayOfWeek),
                    timeSlotId = s.TimeSlotId,
                    slotName = s.TimeSlot.Name,
                    startTime = s.TimeSlot.StartTime.ToString("HH:mm"),
                    endTime = s.TimeSlot.EndTime.ToString("HH:mm")
                })
                .ToList();

            return Json(new
            {
                classId = classItem.Id,
                className = classItem.Name,
                currentTeacherId = classItem.TeacherId,
                startDate = classItem.StartDate.ToString("yyyy-MM-dd"),
                endDate = classItem.EndDate.ToString("yyyy-MM-dd"),
                schedules,
                teachers
            });
        }

        // =========================================================
        // PHÂN CÔNG GIÁO VIÊN
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTeacher(
            AssignTeacherViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    message = "Dữ liệu phân công không hợp lệ."
                });
            }

            var classItem = await _context.Classes
                .Include(c => c.ClassSchedules)
                .FirstOrDefaultAsync(c => c.Id == model.ClassId);

            if (classItem == null)
                return NotFound();

            if (classItem.Status == ClassStatus.Cancelled ||
                classItem.Status == ClassStatus.Completed)
            {
                return BadRequest(new
                {
                    message = "Không thể phân công cho lớp đã hủy " +
                              "hoặc hoàn thành."
                });
            }

            if (!await IsActiveTeacherForSubjectAsync(
                    model.TeacherId, classItem.SubjectId))
            {
                return BadRequest(new
                {
                    message = "Giáo viên không hoạt động " +
                              "hoặc không thuộc môn học của lớp."
                });
            }

            var proposed = classItem.ClassSchedules
                .Select(s => (s.DayOfWeek, s.TimeSlotId))
                .ToList();

            var conflicts = await FindConflictsAsync(
                model.TeacherId,
                classItem.Id,
                classItem.StartDate,
                classItem.EndDate,
                proposed);

            if (conflicts.Count > 0)
            {
                return StatusCode(
                    StatusCodes.Status409Conflict,
                    new
                    {
                        ok = false,
                        conflicts
                    });
            }

            classItem.TeacherId = model.TeacherId;

            if (classItem.Status == ClassStatus.Draft &&
                proposed.Count > 0)
            {
                classItem.Status = ClassStatus.Open;
            }

            classItem.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = $"Đã gán giáo viên cho lớp {classItem.Name}."
            });
        }

        // =========================================================
        // CHI TIẾT SĨ SỐ
        // EnrollmentStatus.Paid = đã thanh toán.
        // EnrollmentStatus.Pending = đang giữ chỗ, chưa hết hạn.
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Capacity(int id)
        {
            var classItem = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (classItem == null)
                return NotFound();

            var now = DateTime.UtcNow;

            var enrollments = await _context.Enrollments
                .AsNoTracking()
                .Where(e =>
                    e.ClassId == id &&
                    (
                        e.Status == EnrollmentStatus.Paid ||
                        (
                            e.Status == EnrollmentStatus.Pending &&
                            (e.ReservedUntil == null ||
                             e.ReservedUntil > now)
                        )
                    ))
                .OrderBy(e => e.Status)
                .ThenBy(e => e.ReservedUntil)
                .Select(e => new
                {
                    studentName = e.Student.FullName,
                    parentId = e.RegisteredByParentId,
                    reservedUntil = e.ReservedUntil,
                    status = e.Status == EnrollmentStatus.Paid
                        ? "Confirmed"
                        : "Reserved"
                })
                .ToListAsync();

            int confirmed = enrollments.Count(e =>
                e.status == "Confirmed");

            int reserved = enrollments.Count(e =>
                e.status == "Reserved");

            int occupied = confirmed + reserved;

            return Json(new
            {
                classId = classItem.Id,
                className = classItem.Name,
                max = classItem.MaxStudents,
                confirmed,
                reserved,
                occupied,
                available = Math.Max(
                    classItem.MaxStudents - occupied, 0),
                isFull = occupied >= classItem.MaxStudents,
                students = enrollments
            });
        }

        // =========================================================
        // CHI TIẾT LỚP
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (!id.HasValue)
                return NotFound();

            var classItem = await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                    .ThenInclude(t => t!.User)
                .Include(c => c.ClassSchedules)
                    .ThenInclude(s => s.TimeSlot)
                .FirstOrDefaultAsync(c => c.Id == id.Value);

            if (classItem == null)
                return NotFound();

            return View("~/Views/Admin/Classes/Details.cshtml", classItem);
        }

        // =========================================================
        // GET: SỬA LỚP
        // Tải lại đúng từng thứ và ca học từ database.
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
                return NotFound();

            var classItem = await _context.Classes
                .AsNoTracking()
                .Include(c => c.ClassSchedules)
                .FirstOrDefaultAsync(c => c.Id == id.Value);

            if (classItem == null)
                return NotFound();

            var model = new ClassViewModel
            {
                Id = classItem.Id,
                SubjectId = classItem.SubjectId,
                TeacherId = classItem.TeacherId,
                Name = classItem.Name,
                StartDate = classItem.StartDate,
                EndDate = classItem.EndDate,
                MaxStudents = classItem.MaxStudents,
                Fee = classItem.Fee,
                Status = classItem.Status,
                Schedules = classItem.ClassSchedules
                    .OrderBy(s => s.DayOfWeek)
                    .Select(s => new ClassScheduleInputViewModel
                    {
                        DayOfWeek = s.DayOfWeek,
                        TimeSlotId = s.TimeSlotId
                    })
                    .ToList()
            };

            await LoadFormDataAsync(
                model.SubjectId,
                model.TeacherId,
                model.Id);

            return View("~/Views/Admin/Classes/Edit.cshtml", model);
        }

        // =========================================================
        // POST: SỬA LỚP + CẬP NHẬT NHIỀU LỊCH HỌC
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ClassViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var classItem = await _context.Classes
                .Include(c => c.ClassSchedules)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (classItem == null)
                return NotFound();

            if (model.Schedules == null)
                model.Schedules = new List<ClassScheduleInputViewModel>();

            model.Name = (model.Name ?? string.Empty).Trim();

            await LoadFormDataAsync(
                model.SubjectId,
                model.TeacherId,
                id);

            IActionResult Back()
            {
                return View("~/Views/Admin/Classes/Edit.cshtml", model);
            }

            ValidateSchedules(model);

            if (model.EndDate < model.StartDate)
            {
                ModelState.AddModelError(
                    nameof(model.EndDate),
                    "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
            }

            if (!ModelState.IsValid)
                return Back();

            if (classItem.Status == ClassStatus.Cancelled)
            {
                TempData["ErrorMessage"] =
                    "Lớp đã hủy không thể chỉnh sửa.";

                return RedirectToAction(nameof(Index));
            }

            bool nameExists = await _context.Classes.AnyAsync(c =>
                c.Name == model.Name && c.Id != id);

            if (nameExists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Tên lớp đã được sử dụng.");

                return Back();
            }

            bool subjectActive = await _context.Subjects.AnyAsync(s =>
                s.Id == model.SubjectId &&
                s.Status == SubjectStatus.Active);

            if (!subjectActive &&
                model.SubjectId != classItem.SubjectId)
            {
                ModelState.AddModelError(
                    nameof(model.SubjectId),
                    "Không thể chuyển sang môn học đã ngừng hoạt động.");

                return Back();
            }

            if (model.TeacherId.HasValue &&
                !await IsActiveTeacherForSubjectAsync(
                    model.TeacherId.Value, model.SubjectId))
            {
                ModelState.AddModelError(
                    nameof(model.TeacherId),
                    "Giáo viên không hoạt động hoặc không thuộc môn học.");

                return Back();
            }

            var slotIds = model.Schedules
                .Select(s => s.TimeSlotId)
                .Distinct()
                .ToList();

            var activeSlotIds = await _context.TimeSlots
                .Where(t =>
                    slotIds.Contains(t.Id) &&
                    t.Status == TimeSlotStatus.Active)
                .Select(t => t.Id)
                .ToListAsync();

            if (activeSlotIds.Count != slotIds.Count)
            {
                ModelState.AddModelError(
                    nameof(model.Schedules),
                    "Có ca học không tồn tại hoặc đã ngừng hoạt động.");

                return Back();
            }

            bool requiresTeacher =
                model.Status == ClassStatus.Open ||
                model.Status == ClassStatus.InProgress;

            if (requiresTeacher && !model.TeacherId.HasValue)
            {
                ModelState.AddModelError(
                    nameof(model.TeacherId),
                    "Phải phân công giáo viên trước khi mở lớp.");

                return Back();
            }

            if (requiresTeacher && model.Schedules.Count == 0)
            {
                ModelState.AddModelError(
                    nameof(model.Schedules),
                    "Lớp phải có ít nhất một lịch học.");

                return Back();
            }

            if (model.TeacherId.HasValue)
            {
                var proposed = model.Schedules
                    .Select(s => (s.DayOfWeek, s.TimeSlotId))
                    .ToList();

                var conflicts = await FindConflictsAsync(
                    model.TeacherId.Value,
                    id,
                    model.StartDate,
                    model.EndDate,
                    proposed);

                if (conflicts.Count > 0)
                {
                    ModelState.AddModelError(
                        nameof(model.TeacherId),
                        "Giáo viên bị trùng lịch: " +
                        DescribeConflicts(conflicts));

                    return Back();
                }
            }

            var now = DateTime.UtcNow;

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                classItem.Name = model.Name;
                classItem.SubjectId = model.SubjectId;
                classItem.TeacherId = model.TeacherId;
                classItem.StartDate = model.StartDate;
                classItem.EndDate = model.EndDate;
                classItem.MaxStudents = model.MaxStudents;
                classItem.Fee = model.Fee;
                classItem.Status = model.Status;
                classItem.UpdatedAt = now;

                _context.ClassSchedules.RemoveRange(
                    classItem.ClassSchedules);

                var newSchedules = model.Schedules.Select(s =>
                    new ClassSchedule
                    {
                        ClassId = classItem.Id,
                        DayOfWeek = s.DayOfWeek,
                        TimeSlotId = s.TimeSlotId,
                        CreatedAt = now,
                        UpdatedAt = now
                    }).ToList();

                _context.ClassSchedules.AddRange(newSchedules);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Đã cập nhật lớp {classItem.Name} và " +
                    $"{newSchedules.Count} lịch học.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "Không thể cập nhật lớp và lịch học.");

                return Back();
            }
        }

        // =========================================================
        // NGỪNG LỚP - SOFT DELETE
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
                return NotFound();

            var classItem = await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                .FirstOrDefaultAsync(c => c.Id == id.Value);

            if (classItem == null)
                return NotFound();

            return View("~/Views/Admin/Classes/Delete.cshtml", classItem);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var classItem = await _context.Classes
                .FirstOrDefaultAsync(c => c.Id == id);

            if (classItem == null)
                return NotFound();

            if (classItem.Status == ClassStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Lớp đã bị hủy.";
                return RedirectToAction(nameof(Index));
            }

            classItem.Status = ClassStatus.Cancelled;
            classItem.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Đã ngừng sử dụng lớp học.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Không thể ngừng lớp học.";
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // DANH SÁCH GIÁO VIÊN THEO MÔN HỌC
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetTeachersBySubject(int subjectId)
        {
            if (subjectId <= 0)
                return Json(Array.Empty<object>());

            var teachers = await _context.TeacherProfiles
                .AsNoTracking()
                .Where(t =>
                    t.SubjectId == subjectId &&
                    t.User.Status == UserStatus.Active &&
                    t.User.Role == UserRole.Teacher)
                .OrderBy(t => t.FullName)
                .Select(t => new
                {
                    id = t.Id,
                    fullName = t.FullName
                })
                .ToListAsync();

            return Json(teachers);
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private async Task<List<Class>> BuildIndexAsync()
        {
            await LoadFormDataAsync();

            return await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                .Include(c => c.ClassSchedules)
                    .ThenInclude(s => s.TimeSlot)
                .OrderBy(c => c.Id)
                .ToListAsync();
        }

        private async Task LoadFormDataAsync(
            int? selectedSubjectId = null,
            int? selectedTeacherId = null,
            int? excludeClassId = null)
        {
            var subjects = await _context.Subjects
                .AsNoTracking()
                .Where(s => s.Status == SubjectStatus.Active)
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewBag.Subjects = new SelectList(
                subjects,
                nameof(Subject.Id),
                nameof(Subject.Name),
                selectedSubjectId);

            var teachersQuery = _context.TeacherProfiles
                .AsNoTracking()
                .Where(t =>
                    t.User.Status == UserStatus.Active &&
                    t.User.Role == UserRole.Teacher);

            if (selectedSubjectId.HasValue)
            {
                teachersQuery = teachersQuery.Where(t =>
                    t.SubjectId == selectedSubjectId.Value);
            }

            var teachers = await teachersQuery
                .OrderBy(t => t.FullName)
                .ToListAsync();

            ViewBag.Teachers = new SelectList(
                teachers,
                nameof(TeacherProfile.Id),
                nameof(TeacherProfile.FullName),
                selectedTeacherId);

            var slots = await _context.TimeSlots
                .AsNoTracking()
                .Where(t => t.Status == TimeSlotStatus.Active)
                .OrderBy(t => t.StartTime)
                .ToListAsync();

            ViewBag.TimeSlots = slots.Select(t =>
                new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = $"{t.Name} " +
                           $"({t.StartTime:HH\\:mm} - {t.EndTime:HH\\:mm})"
                }).ToList();
        }

        private void ValidateSchedules(ClassViewModel model)
        {
            if (model.Schedules == null ||
                model.Schedules.Count == 0)
            {
                ModelState.AddModelError(
                    nameof(model.Schedules),
                    "Vui lòng thêm ít nhất một lịch học.");

                return;
            }

            if (model.Schedules.Any(s => s == null))
            {
                ModelState.AddModelError(
                    nameof(model.Schedules),
                    "Có lịch học không hợp lệ.");

                return;
            }

            if (model.Schedules.Any(s =>
                s.DayOfWeek < 2 || s.DayOfWeek > 8))
            {
                ModelState.AddModelError(
                    nameof(model.Schedules),
                    "Thứ học phải từ 2 đến 8.");

                return;
            }

            if (model.Schedules.Any(s => s.TimeSlotId <= 0))
            {
                ModelState.AddModelError(
                    nameof(model.Schedules),
                    "Vui lòng chọn ca học cho từng thứ.");

                return;
            }

            if (model.Schedules.Select(s => s.DayOfWeek)
                .Distinct().Count() != model.Schedules.Count)
            {
                ModelState.AddModelError(
                    nameof(model.Schedules),
                    "Mỗi thứ chỉ được cấu hình một lần.");
            }
        }

        private async Task<IActionResult> ReturnCreateErrorAsync()
        {
            var message = string.Join(
                " ",
                ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(e => !string.IsNullOrWhiteSpace(e)));

            TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(message)
                ? "Dữ liệu lớp học không hợp lệ."
                : message;

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> IsActiveTeacherForSubjectAsync(
            int teacherId,
            int subjectId)
        {
            return await _context.TeacherProfiles.AnyAsync(t =>
                t.Id == teacherId &&
                t.SubjectId == subjectId &&
                t.User.Status == UserStatus.Active &&
                t.User.Role == UserRole.Teacher);
        }

        private static string DayLabel(int day)
        {
            return day == 8 ? "CN" : $"Thứ {day}";
        }

        private static string DescribeConflicts(
            IEnumerable<ConflictInfo> conflicts)
        {
            return string.Join(
                "; ",
                conflicts.Select(c =>
                    $"lớp {c.ClassName} ({c.DayLabel}, {c.SlotName})"));
        }

        private async Task<List<ConflictInfo>> FindConflictsAsync(
            int teacherId,
            int? currentClassId,
            DateOnly start,
            DateOnly end,
            List<(int Day, int SlotId)> proposed)
        {
            var result = new List<ConflictInfo>();

            if (proposed.Count == 0)
                return result;

            var slotIds = proposed
                .Select(p => p.SlotId)
                .Distinct()
                .ToList();

            var slots = await _context.TimeSlots
                .AsNoTracking()
                .Where(t => slotIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id);

            foreach (var group in proposed.GroupBy(p => p.SlotId))
            {
                if (!slots.TryGetValue(group.Key, out var proposedSlot))
                    continue;

                var days = group
                    .Select(g => g.Day)
                    .Distinct()
                    .ToList();

                var found = await _context.ClassSchedules
                    .AsNoTracking()
                    .Where(s =>
                        s.Class.TeacherId == teacherId &&
                        (!currentClassId.HasValue ||
                         s.ClassId != currentClassId.Value) &&
                        s.Class.Status != ClassStatus.Cancelled &&
                        s.Class.Status != ClassStatus.Completed &&
                        s.Class.StartDate <= end &&
                        s.Class.EndDate >= start &&
                        days.Contains(s.DayOfWeek) &&
                        s.TimeSlot.StartTime < proposedSlot.EndTime &&
                        s.TimeSlot.EndTime > proposedSlot.StartTime)
                    .Select(s => new
                    {
                        ClassName = s.Class.Name,
                        s.DayOfWeek,
                        SlotName = s.TimeSlot.Name
                    })
                    .ToListAsync();

                result.AddRange(found.Select(f => new ConflictInfo
                {
                    ClassName = f.ClassName,
                    DayOfWeek = f.DayOfWeek,
                    DayLabel = DayLabel(f.DayOfWeek),
                    SlotName = f.SlotName
                }));
            }

            return result
                .GroupBy(c => (c.ClassName, c.DayOfWeek, c.SlotName))
                .Select(g => g.First())
                .OrderBy(c => c.DayOfWeek)
                .ToList();
        }

        private sealed class ConflictInfo
        {
            public string ClassName { get; set; } = string.Empty;
            public int DayOfWeek { get; set; }
            public string DayLabel { get; set; } = string.Empty;
            public string SlotName { get; set; } = string.Empty;
        }
    }
}
