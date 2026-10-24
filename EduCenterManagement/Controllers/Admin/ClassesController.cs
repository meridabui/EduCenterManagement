
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
            await LoadFormDataAsync();

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/Classes/Create.cshtml",
                    model);
            }

            var name = model.Name.Trim();

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

            if (model.TeacherId.HasValue)
            {
                bool teacherExists = await _context.TeacherProfiles
                    .AnyAsync(t =>
                        t.Id == model.TeacherId.Value &&
                        t.User.Status == UserStatus.Active &&
                        t.User.Role == UserRole.Teacher);

                if (!teacherExists)
                {
                    ModelState.AddModelError(
                        nameof(model.TeacherId),
                        "Giáo viên không tồn tại hoặc không hoạt động.");

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

            await LoadFormDataAsync(
                model.SubjectId,
                model.TeacherId);

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/Classes/Edit.cshtml",
                    model);
            }

            var classItem = await _context.Classes
                .FirstOrDefaultAsync(c => c.Id == id);

            if (classItem == null)
                return NotFound();

            bool nameExists = await _context.Classes
                .AnyAsync(c => c.Name == model.Name.Trim() && c.Id != id);

            if (nameExists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Tên lớp đã được sử dụng.");

                return View(
                    "~/Views/Admin/Classes/Edit.cshtml",
                    model);
            }

            bool subjectExists = await _context.Subjects
                .AnyAsync(s =>
                    s.Id == model.SubjectId &&
                    s.Status == SubjectStatus.Active);

            // Cho phép giữ Subject hiện tại nếu nó đã Inactive.
            if (!subjectExists && classItem.SubjectId != model.SubjectId)
            {
                ModelState.AddModelError(
                    nameof(model.SubjectId),
                    "Không thể chuyển lớp sang môn học không hoạt động.");

                return View(
                    "~/Views/Admin/Classes/Edit.cshtml",
                    model);
            }

            if (model.TeacherId.HasValue)
            {
                bool teacherExists = await _context.TeacherProfiles
                    .AnyAsync(t =>
                        t.Id == model.TeacherId.Value &&
                        t.User.Status == UserStatus.Active &&
                        t.User.Role == UserRole.Teacher);

                if (!teacherExists)
                {
                    ModelState.AddModelError(
                        nameof(model.TeacherId),
                        "Giáo viên không tồn tại hoặc không hoạt động.");

                    return View(
                        "~/Views/Admin/Classes/Edit.cshtml",
                        model);
                }
            }

            classItem.Name = model.Name.Trim();
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

            TempData["SuccessMessage"] = "Cập nhật lớp học thành công.";

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

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Đã hủy lớp học. Dữ liệu và lịch sử vẫn được giữ nguyên.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadFormDataAsync(
            int? selectedSubjectId = null,
            int? selectedTeacherId = null)
        {
            var subjects = await _context.Subjects
                .AsNoTracking()
                .Where(s => s.Status == SubjectStatus.Active)
                .OrderBy(s => s.Name)
                .ToListAsync();

            // Khi sửa lớp, vẫn cho hiển thị Subject hiện tại
            // nếu Subject đã Inactive.
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

            var teachers = await _context.TeacherProfiles
                .AsNoTracking()
                .Where(t =>
                    t.User.Status == UserStatus.Active &&
                    t.User.Role == UserRole.Teacher)
                .OrderBy(t => t.FullName)
                .ToListAsync();

            // Khi sửa lớp, vẫn hiển thị Teacher đang được phân công,
            // kể cả khi tài khoản Teacher đã ngừng hoạt động.
            if (selectedTeacherId.HasValue &&
                !teachers.Any(t => t.Id == selectedTeacherId.Value))
            {
                var currentTeacher = await _context.TeacherProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => t.Id == selectedTeacherId.Value);

                if (currentTeacher != null)
                    teachers.Add(currentTeacher);
            }

            ViewBag.Teachers = new SelectList(
                teachers,
                nameof(TeacherProfile.Id),
                nameof(TeacherProfile.FullName),
                selectedTeacherId);
        }
    }
}
