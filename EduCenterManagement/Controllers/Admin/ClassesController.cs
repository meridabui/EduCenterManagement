
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

        // GET: /Classes
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var classes = await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                    .ThenInclude(t => t!.User)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View("~/Views/Admin/Classes/Index.cshtml", classes);
        }

        // GET: /Classes/Details/1
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var classItem = await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                    .ThenInclude(t => t!.User)
                .FirstOrDefaultAsync(c => c.Id == id.Value);

            if (classItem == null)
                return NotFound();

            return View(
                "~/Views/Admin/Classes/Details.cshtml",
                classItem);
        }

        // GET: /Classes/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadFormDataAsync();

            return View(
                "~/Views/Admin/Classes/Create.cshtml",
                new ClassViewModel());
        }

        // POST: /Classes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClassViewModel model)
        {
            await LoadFormDataAsync(
                model.SubjectId,
                model.TeacherId);

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/Classes/Create.cshtml",
                    model);
            }

            var name = model.Name.Trim();

            // Kiểm tra tên lớp trùng.
            bool nameExists = await _context.Classes
                .AnyAsync(c => c.Name == name);

            if (nameExists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Tên lớp đã tồn tại.");

                return View(
                    "~/Views/Admin/Classes/Create.cshtml",
                    model);
            }

            // Subject mới phải đang hoạt động.
            bool subjectExists = await _context.Subjects
                .AnyAsync(s =>
                    s.Id == model.SubjectId &&
                    s.Status == SubjectStatus.Active);

            if (!subjectExists)
            {
                ModelState.AddModelError(
                    nameof(model.SubjectId),
                    "Môn học không tồn tại hoặc đã ngừng hoạt động.");

                return View(
                    "~/Views/Admin/Classes/Create.cshtml",
                    model);
            }

            // Nếu có chọn Teacher, kiểm tra tài khoản.
            if (model.TeacherId.HasValue)
            {
                bool teacherExists = await IsActiveTeacherForSubjectAsync(
                    model.TeacherId.Value,model.SubjectId);

                if (!teacherExists)
                {
                    ModelState.AddModelError(
                        nameof(model.TeacherId),
                        "Giáo viên không tồn tại hoặc tài khoản không hoạt động.");

                    return View(
                        "~/Views/Admin/Classes/Create.cshtml",
                        model);
                }
            }

            var now = DateTime.UtcNow;

            var classItem = new Class
            {
                Name = name,
                SubjectId = model.SubjectId,
                TeacherId = model.TeacherId,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                MaxStudents = model.MaxStudents,
                Fee = model.Fee,

                // Lớp mới luôn bắt đầu ở trạng thái Draft.
                Status = ClassStatus.Draft,

                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Classes.Add(classItem);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể tạo lớp. Vui lòng kiểm tra dữ liệu.");

                return View(
                    "~/Views/Admin/Classes/Create.cshtml",
                    model);
            }

            TempData["SuccessMessage"] = "Tạo lớp học thành công.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Classes/Edit/1
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var classItem = await _context.Classes
                .AsNoTracking()
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
                Status = classItem.Status
            };

            await LoadFormDataAsync(
                model.SubjectId,
                model.TeacherId);

            return View(
                "~/Views/Admin/Classes/Edit.cshtml",
                model);
        }

        // POST: /Classes/Edit/1
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ClassViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var classItem = await _context.Classes
                .FirstOrDefaultAsync(c => c.Id == id);

            if (classItem == null)
                return NotFound();

            await LoadFormDataAsync(
                model.SubjectId,
                model.TeacherId);

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/Classes/Edit.cshtml",
                    model);
            }

            // Không cho sửa lớp đã Cancelled.
            if (classItem.Status == ClassStatus.Cancelled)
            {
                TempData["ErrorMessage"] =
                    "Lớp đã hủy không thể chỉnh sửa.";

                return RedirectToAction(nameof(Index));
            }

            var name = model.Name.Trim();

            // Kiểm tra tên lớp trùng.
            bool nameExists = await _context.Classes
                .AnyAsync(c => c.Name == name && c.Id != id);

            if (nameExists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Tên lớp đã được sử dụng.");

                return View(
                    "~/Views/Admin/Classes/Edit.cshtml",
                    model);
            }

            // Kiểm tra Subject.
            bool subjectExists = await _context.Subjects
                .AnyAsync(s =>
                    s.Id == model.SubjectId &&
                    s.Status == SubjectStatus.Active);

            // Cho phép giữ nguyên Subject cũ nếu đã Inactive.
            if (!subjectExists && classItem.SubjectId != model.SubjectId)
            {
                ModelState.AddModelError(
                    nameof(model.SubjectId),
                    "Không thể chuyển lớp sang môn học không hoạt động.");

                return View(
                    "~/Views/Admin/Classes/Edit.cshtml",
                    model);
            }

            // Kiểm tra Teacher nếu có chọn.
            if (model.TeacherId.HasValue)
            {
                bool teacherExists = await IsActiveTeacherForSubjectAsync(
                     model.TeacherId.Value,model.SubjectId);

                if (!teacherExists)
                {
                    ModelState.AddModelError(
                        nameof(model.TeacherId),
                        "Giáo viên không hoạt động hoặc không thuộc môn học của lớp.");

                    return View(
                        "~/Views/Admin/Classes/Edit.cshtml",
                        model);
                }
            }

            bool teacherChanged =
                model.TeacherId != classItem.TeacherId;

            bool requiresTeacherAndSchedule =
                model.Status == ClassStatus.Open ||
                model.Status == ClassStatus.InProgress;

            // Không cho mở lớp hoặc bắt đầu học nếu chưa có Teacher.
            if (requiresTeacherAndSchedule &&
                !model.TeacherId.HasValue)
            {
                ModelState.AddModelError(
                    nameof(model.TeacherId),
                    "Phải phân công Teacher trước khi mở lớp hoặc bắt đầu học.");

                return View(
                    "~/Views/Admin/Classes/Edit.cshtml",
                    model);
            }

            // Chỉ Open/InProgress mới bắt buộc có lịch hợp lệ.
            if (requiresTeacherAndSchedule)
            {
                bool hasValidSchedule = await _context.ClassSchedules
                    .AnyAsync(s =>
                        s.ClassId == id &&
                        s.TimeSlot.Status == TimeSlotStatus.Active);

                if (!hasValidSchedule)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Phải thiết lập ít nhất một lịch học sử dụng " +
                        "TimeSlot đang hoạt động trước khi mở lớp.");

                    return View(
                        "~/Views/Admin/Classes/Edit.cshtml",
                        model);
                }
            }

            // Kiểm tra xung đột khi đổi Teacher hoặc mở lớp.
            bool datesChanged =model.StartDate != classItem.StartDate ||model.EndDate != classItem.EndDate;

            if (model.TeacherId.HasValue &&
                (teacherChanged || datesChanged || requiresTeacherAndSchedule))
            {
                bool conflict = await HasTeacherScheduleConflictAsync(
                    id,
                    model.TeacherId.Value,
                    model.StartDate,
                    model.EndDate);

                if (conflict)
                {
                    ModelState.AddModelError(
                        nameof(model.TeacherId),
                        "Giáo viên đã có lịch dạy trùng với lớp này " +
                        "trong thời gian khóa học.");

                    return View(
                        "~/Views/Admin/Classes/Edit.cshtml",
                        model);
                }
            }

            classItem.Name = name;
            classItem.SubjectId = model.SubjectId;
            classItem.TeacherId = model.TeacherId;
            classItem.StartDate = model.StartDate;
            classItem.EndDate = model.EndDate;
            classItem.MaxStudents = model.MaxStudents;
            classItem.Fee = model.Fee;
            classItem.Status = model.Status;
            classItem.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể cập nhật lớp. Vui lòng kiểm tra dữ liệu.");

                return View(
                    "~/Views/Admin/Classes/Edit.cshtml",
                    model);
            }

            TempData["SuccessMessage"] =
                "Cập nhật lớp học và phân công Teacher thành công.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Classes/Delete/1
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var classItem = await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                    .ThenInclude(t => t!.User)
                .FirstOrDefaultAsync(c => c.Id == id.Value);

            if (classItem == null)
                return NotFound();

            return View(
                "~/Views/Admin/Classes/Delete.cshtml",
                classItem);
        }

        // POST: /Classes/Delete/1
        // Soft Delete: chuyển trạng thái sang Cancelled.
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
                TempData["ErrorMessage"] = "Lớp học đã bị hủy.";

                return RedirectToAction(nameof(Index));
            }

            classItem.Status = ClassStatus.Cancelled;
            classItem.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Không thể hủy lớp. Vui lòng thử lại.";

                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                "Đã hủy lớp học. Dữ liệu và lịch sử vẫn được giữ nguyên.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> IsActiveTeacherForSubjectAsync(
            int teacherId,
            int subjectId)
        {
            return await _context.TeacherProfiles
                .AnyAsync(t =>
                    t.Id == teacherId &&
                    t.SubjectId == subjectId &&
                    t.User.Status == UserStatus.Active &&
                    t.User.Role == UserRole.Teacher);
        }


        // Kiểm tra lịch Teacher, gồm thứ, giờ thực tế và khoảng ngày khóa học.
        private async Task<bool> HasTeacherScheduleConflictAsync(
            int currentClassId,
            int teacherId,
            DateOnly proposedStartDate,
            DateOnly proposedEndDate)
        {
            var targetSchedules = await _context.ClassSchedules
                .AsNoTracking()
                .Where(s => s.ClassId == currentClassId)
                .Select(s => new
                {
                    s.DayOfWeek,
                    s.TimeSlot.StartTime,
                    s.TimeSlot.EndTime
                })
                .ToListAsync();

            // Chưa có lịch thì chưa thể xác định xung đột.
            if (targetSchedules.Count == 0)
                return false;

            foreach (var target in targetSchedules)
            {
                bool conflict = await _context.ClassSchedules
                    .AsNoTracking()
                    .AnyAsync(other =>
                        other.ClassId != currentClassId &&
                        other.DayOfWeek == target.DayOfWeek &&
                        other.Class.TeacherId == teacherId &&
                        other.Class.Status != ClassStatus.Cancelled &&
                        other.Class.Status != ClassStatus.Completed &&

                        // Hai khóa học có khoảng ngày giao nhau.
                        other.Class.StartDate <= proposedEndDate &&
                        other.Class.EndDate >= proposedStartDate &&

                        // Hai khung giờ thực tế bị chồng lấn.
                        other.TimeSlot.StartTime < target.EndTime &&
                        other.TimeSlot.EndTime > target.StartTime);

                if (conflict)
                    return true;
            }

            return false;
        }


        [HttpGet]
        public async Task<IActionResult> GetTeachersBySubject(int subjectId)
        {
            if (subjectId <= 0)
                return Json(Array.Empty<object>());

            bool subjectExists = await _context.Subjects
                .AnyAsync(s =>
                    s.Id == subjectId &&
                    s.Status == SubjectStatus.Active);

            if (!subjectExists)
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



        // Nạp dữ liệu cho dropdown Subject và Teacher.
        private async Task LoadFormDataAsync(
            int? selectedSubjectId = null,
            int? selectedTeacherId = null)
        {
            var subjects = await _context.Subjects
                .AsNoTracking()
                .Where(s => s.Status == SubjectStatus.Active)
                .OrderBy(s => s.Name)
                .ToListAsync();

            // Giữ Subject hiện tại nếu đã Inactive để xem/sửa lớp cũ.
            if (selectedSubjectId.HasValue &&
                !subjects.Any(s => s.Id == selectedSubjectId.Value))
            {
                var currentSubject = await _context.Subjects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.Id == selectedSubjectId.Value);

                if (currentSubject != null)
                    subjects.Add(currentSubject);
            }

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
                teachersQuery = teachersQuery.Where(
                    t => t.SubjectId == selectedSubjectId.Value);
            }

            var teachers = await teachersQuery
                .OrderBy(t => t.FullName)
                .ToListAsync();


            ViewBag.Teachers = new SelectList(
                teachers,
                nameof(TeacherProfile.Id),
                nameof(TeacherProfile.FullName),
                selectedTeacherId);
        }
    }
}
