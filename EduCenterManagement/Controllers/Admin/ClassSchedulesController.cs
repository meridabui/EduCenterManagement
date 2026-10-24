
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
    public class ClassSchedulesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClassSchedulesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /ClassSchedules
        public async Task<IActionResult> Index()
        {
            var schedules = await _context.ClassSchedules
                .AsNoTracking()
                .Include(s => s.Class)
                    .ThenInclude(c => c.Subject)
                .Include(s => s.Class)
                    .ThenInclude(c => c.Teacher)
                .Include(s => s.TimeSlot)
                .OrderBy(s => s.Class.Name)
                .ThenBy(s => s.DayOfWeek)
                .ThenBy(s => s.TimeSlot.StartTime)
                .ToListAsync();

            return View(
                "~/Views/Admin/ClassSchedules/Index.cshtml",
                schedules);
        }

        // GET: /ClassSchedules/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadFormDataAsync();

            return View(
                "~/Views/Admin/ClassSchedules/Create.cshtml",
                new ClassScheduleViewModel());
        }

        // POST: /ClassSchedules/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            ClassScheduleViewModel model)
        {
            await LoadFormDataAsync(model.ClassId, model.TimeSlotId);

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/ClassSchedules/Create.cshtml",
                    model);
            }

            var classItem = await _context.Classes
                .Include(c => c.Teacher)
                .FirstOrDefaultAsync(c => c.Id == model.ClassId);

            if (classItem == null)
            {
                ModelState.AddModelError(
                    nameof(model.ClassId),
                    "Lớp học không tồn tại.");

                return View(
                    "~/Views/Admin/ClassSchedules/Create.cshtml",
                    model);
            }

            if (classItem.Status == ClassStatus.Cancelled ||
                classItem.Status == ClassStatus.Completed)
            {
                ModelState.AddModelError(
                    nameof(model.ClassId),
                    "Không thể thiết lập lịch cho lớp đã hủy hoặc hoàn thành.");

                return View(
                    "~/Views/Admin/ClassSchedules/Create.cshtml",
                    model);
            }

            var timeSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(t =>
                    t.Id == model.TimeSlotId &&
                    t.Status == TimeSlotStatus.Active);

            if (timeSlot == null)
            {
                ModelState.AddModelError(
                    nameof(model.TimeSlotId),
                    "Khung giờ không tồn tại hoặc đã ngừng hoạt động.");

                return View(
                    "~/Views/Admin/ClassSchedules/Create.cshtml",
                    model);
            }

            bool duplicate = await _context.ClassSchedules
                .AnyAsync(s =>
                    s.ClassId == model.ClassId &&
                    s.DayOfWeek == model.DayOfWeek &&
                    s.TimeSlotId == model.TimeSlotId);

            if (duplicate)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Lớp đã có lịch học vào thứ và khung giờ này.");

                return View(
                    "~/Views/Admin/ClassSchedules/Create.cshtml",
                    model);
            }

            // Nếu lớp đã có Teacher, kiểm tra lịch của Teacher đó.
            if (classItem.TeacherId.HasValue)
            {
                bool teacherConflict = await _context.ClassSchedules
                    .Include(s => s.Class)
                    .AnyAsync(s =>
                        s.DayOfWeek == model.DayOfWeek &&
                        s.TimeSlotId == model.TimeSlotId &&
                        s.Class.TeacherId == classItem.TeacherId &&
                        s.ClassId != model.ClassId);

                if (teacherConflict)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Teacher đã được phân công lớp khác cùng thứ và khung giờ.");

                    return View(
                        "~/Views/Admin/ClassSchedules/Create.cshtml",
                        model);
                }
            }

            var now = DateTime.UtcNow;

            var schedule = new ClassSchedule
            {
                ClassId = model.ClassId,
                TimeSlotId = model.TimeSlotId,
                DayOfWeek = model.DayOfWeek,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.ClassSchedules.Add(schedule);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể lưu lịch học. Vui lòng kiểm tra lại.");

                return View(
                    "~/Views/Admin/ClassSchedules/Create.cshtml",
                    model);
            }

            TempData["SuccessMessage"] = "Tạo lịch học thành công.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /ClassSchedules/Edit/1
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var schedule = await _context.ClassSchedules
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id.Value);

            if (schedule == null)
                return NotFound();

            var model = new ClassScheduleViewModel
            {
                Id = schedule.Id,
                ClassId = schedule.ClassId,
                TimeSlotId = schedule.TimeSlotId,
                DayOfWeek = schedule.DayOfWeek
            };

            await LoadFormDataAsync(model.ClassId, model.TimeSlotId);

            return View(
                "~/Views/Admin/ClassSchedules/Edit.cshtml",
                model);
        }

        // POST: /ClassSchedules/Edit/1
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ClassScheduleViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            await LoadFormDataAsync(model.ClassId, model.TimeSlotId);

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/ClassSchedules/Edit.cshtml",
                    model);
            }

            var schedule = await _context.ClassSchedules
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (schedule == null)
                return NotFound();

            var classItem = await _context.Classes
                .FirstOrDefaultAsync(c => c.Id == model.ClassId);

            if (classItem == null ||
                classItem.Status == ClassStatus.Cancelled ||
                classItem.Status == ClassStatus.Completed)
            {
                ModelState.AddModelError(
                    nameof(model.ClassId),
                    "Lớp học không hợp lệ hoặc không còn hoạt động.");

                return View(
                    "~/Views/Admin/ClassSchedules/Edit.cshtml",
                    model);
            }

            var timeSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(t =>
                    t.Id == model.TimeSlotId &&
                    t.Status == TimeSlotStatus.Active);

            if (timeSlot == null)
            {
                ModelState.AddModelError(
                    nameof(model.TimeSlotId),
                    "Khung giờ không tồn tại hoặc đã ngừng hoạt động.");

                return View(
                    "~/Views/Admin/ClassSchedules/Edit.cshtml",
                    model);
            }

            bool duplicate = await _context.ClassSchedules
                .AnyAsync(s =>
                    s.Id != id &&
                    s.ClassId == model.ClassId &&
                    s.DayOfWeek == model.DayOfWeek &&
                    s.TimeSlotId == model.TimeSlotId);

            if (duplicate)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Lịch học này đã tồn tại.");

                return View(
                    "~/Views/Admin/ClassSchedules/Edit.cshtml",
                    model);
            }

            if (classItem.TeacherId.HasValue)
            {
                bool teacherConflict = await _context.ClassSchedules
                    .Include(s => s.Class)
                    .AnyAsync(s =>
                        s.Id != id &&
                        s.DayOfWeek == model.DayOfWeek &&
                        s.TimeSlotId == model.TimeSlotId &&
                        s.Class.TeacherId == classItem.TeacherId &&
                        s.ClassId != model.ClassId);

                if (teacherConflict)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Teacher đã có lớp khác cùng thứ và khung giờ.");

                    return View(
                        "~/Views/Admin/ClassSchedules/Edit.cshtml",
                        model);
                }
            }

            schedule.ClassId = model.ClassId;
            schedule.TimeSlotId = model.TimeSlotId;
            schedule.DayOfWeek = model.DayOfWeek;
            schedule.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể cập nhật lịch học.");

                return View(
                    "~/Views/Admin/ClassSchedules/Edit.cshtml",
                    model);
            }

            TempData["SuccessMessage"] = "Cập nhật lịch học thành công.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadFormDataAsync(
            int? selectedClassId = null,
            int? selectedTimeSlotId = null)
        {
            var classes = await _context.Classes
                .AsNoTracking()
                .Where(c =>
                    c.Status != ClassStatus.Cancelled &&
                    c.Status != ClassStatus.Completed)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // Khi sửa, giữ lớp hiện tại trong danh sách
            // nếu lớp đã chuyển trạng thái.
            if (selectedClassId.HasValue &&
                !classes.Any(c => c.Id == selectedClassId.Value))
            {
                var currentClass = await _context.Classes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == selectedClassId.Value);

                if (currentClass != null)
                    classes.Add(currentClass);
            }

            ViewBag.Classes = new SelectList(
                classes,
                nameof(Class.Id),
                nameof(Class.Name),
                selectedClassId);

            var timeSlots = await _context.TimeSlots
                .AsNoTracking()
                .Where(t => t.Status == TimeSlotStatus.Active)
                .OrderBy(t => t.StartTime)
                .ToListAsync();

            // Khi sửa, giữ khung giờ hiện tại để xem dữ liệu cũ.
            if (selectedTimeSlotId.HasValue &&
                !timeSlots.Any(t => t.Id == selectedTimeSlotId.Value))
            {
                var currentTimeSlot = await _context.TimeSlots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == selectedTimeSlotId.Value);

                if (currentTimeSlot != null)
                    timeSlots.Add(currentTimeSlot);
            }

            ViewBag.TimeSlots = new SelectList(
                timeSlots,
                nameof(TimeSlot.Id),
                nameof(TimeSlot.Name),
                selectedTimeSlotId);

            ViewBag.DaysOfWeek = new SelectList(
                new[]
                {
                    new { Value = 2, Text = "Thứ Hai" },
                    new { Value = 3, Text = "Thứ Ba" },
                    new { Value = 4, Text = "Thứ Tư" },
                    new { Value = 5, Text = "Thứ Năm" },
                    new { Value = 6, Text = "Thứ Sáu" },
                    new { Value = 7, Text = "Thứ Bảy" },
                    new { Value = 8, Text = "Chủ nhật" }
                },
                "Value",
                "Text");
        }
    }
}
