
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace EduCenterManagement.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class TeachersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public TeachersController(
            ApplicationDbContext context,
            IPasswordHasher<AppUser> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // GET: /Teachers
        public async Task<IActionResult> Index(int page = 1, int? subjectId = null)
        {
            ViewBag.Subjects = new SelectList(await _context.Subjects.AsNoTracking()
                .OrderBy(s => s.Name).ToListAsync(), "Id", "Name", subjectId);
            ViewBag.SelectedSubjectId = subjectId;

            var query = _context.TeacherProfiles.AsNoTracking();
            if (subjectId.HasValue)
                query = query.Where(t => t.TeacherSubjects.Any(s => s.SubjectId == subjectId.Value));

            var teachers = await query
                .Include(t => t.User)
                .Include(t => t.TeacherSubjects)
                    .ThenInclude(ts => ts.Subject)
                .ToListAsync();

            var nameComparer = StringComparer.Create(
                System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: true);
            var sortedTeachers = teachers
                .OrderBy(t => t.FullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault() ?? string.Empty, nameComparer)
                .ThenBy(t => t.FullName.Trim(), nameComparer)
                .ThenBy(t => t.Id)
                .ToList();

            var pagination = new PaginationViewModel(sortedTeachers.Count, page);
            ViewBag.Pagination = pagination;
            return View("~/Views/Admin/Teachers/Index.cshtml", sortedTeachers
                .Skip(pagination.Offset).Take(PaginationViewModel.PageSize).ToList());
        }

        // GET: /Teachers/Create

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadSubjectsAsync();

            return View(
                "~/Views/Admin/Teachers/Create.cshtml",
                new CreateTeacherViewModel());
        }


        // POST: /Teachers/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTeacherViewModel model)
        {
            model.SubjectIds = (model.SubjectIds ?? new List<int>()).Distinct().ToList();
            await LoadSubjectsAsync(model.SubjectIds);

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/Teachers/Create.cshtml",
                    model);
            }

            var email = model.Email.Trim().ToLowerInvariant();

            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email == email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Email này đã được sử dụng.");

                return View(
                    "~/Views/Admin/Teachers/Create.cshtml",
                    model);
            }

            if (!await ValidateSubjectsAsync(model.SubjectIds))
            {
                return View(
                    "~/Views/Admin/Teachers/Create.cshtml",
                    model);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;

                var user = new AppUser
                {
                    Email = email,
                    Role = UserRole.Teacher,
                    Status = UserStatus.Active,
                    EmailVerifiedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                user.PasswordHash = _passwordHasher.HashPassword(
                    user, model.Password);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var teacher = new TeacherProfile
                {
                    UserId = user.Id,
                    TeacherSubjects = model.SubjectIds
                        .Select(id => new TeacherSubject { SubjectId = id })
                        .ToList(),
                    FullName = model.FullName.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.TeacherProfiles.Add(teacher);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    "Tạo tài khoản Teacher và phân công các môn học thành công.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "Không thể tạo tài khoản Teacher. Vui lòng kiểm tra dữ liệu.");

                return View(
                    "~/Views/Admin/Teachers/Create.cshtml",
                    model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> AssignSubjects(int id)
        {
            var teacher = await _context.TeacherProfiles
                .AsNoTracking()
                .Include(t => t.TeacherSubjects)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null)
                return NotFound();

            var model = new AssignTeacherSubjectsViewModel
            {
                TeacherId = teacher.Id,
                SubjectIds = teacher.TeacherSubjects.Select(ts => ts.SubjectId).ToList()
            };

            ViewBag.TeacherName = teacher.FullName;
            await LoadSubjectsAsync(model.SubjectIds, model.SubjectIds);
            return View("~/Views/Admin/Teachers/AssignSubjects.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignSubjects(AssignTeacherSubjectsViewModel model)
        {
            var teacher = await _context.TeacherProfiles
                .Include(t => t.TeacherSubjects)
                .FirstOrDefaultAsync(t => t.Id == model.TeacherId);

            if (teacher == null)
                return NotFound();

            var currentSubjectIds = teacher.TeacherSubjects.Select(ts => ts.SubjectId).ToList();
            model.SubjectIds = (model.SubjectIds ?? new List<int>()).Distinct().ToList();
            ViewBag.TeacherName = teacher.FullName;
            await LoadSubjectsAsync(model.SubjectIds, currentSubjectIds);

            if (!ModelState.IsValid ||
                !await ValidateSubjectsAsync(model.SubjectIds, currentSubjectIds))
            {
                return View("~/Views/Admin/Teachers/AssignSubjects.cshtml", model);
            }

            var removedSubjectIds = currentSubjectIds.Except(model.SubjectIds).ToList();
            var blockingClasses = await _context.Classes
                .AsNoTracking()
                .Where(c => c.TeacherId == teacher.Id &&
                    removedSubjectIds.Contains(c.SubjectId) &&
                    c.Status != ClassStatus.Completed &&
                    c.Status != ClassStatus.Cancelled)
                .OrderBy(c => c.Name)
                .Select(c => c.Name)
                .ToListAsync();

            if (blockingClasses.Count > 0)
            {
                ModelState.AddModelError(nameof(model.SubjectIds),
                    "Không thể bỏ môn vì giáo viên vẫn phụ trách các lớp: " +
                    string.Join(", ", blockingClasses) +
                    ". Hãy phân công lại giáo viên hoặc hoàn tất xử lý các lớp trước.");
                return View("~/Views/Admin/Teachers/AssignSubjects.cshtml", model);
            }

            _context.TeacherSubjects.RemoveRange(teacher.TeacherSubjects
                .Where(ts => removedSubjectIds.Contains(ts.SubjectId)).ToList());

            foreach (var subjectId in model.SubjectIds.Except(currentSubjectIds))
            {
                teacher.TeacherSubjects.Add(new TeacherSubject { SubjectId = subjectId });
            }

            teacher.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty,
                    "Không thể cập nhật môn được phân công. Vui lòng tải lại trang và thử lại.");
                return View("~/Views/Admin/Teachers/AssignSubjects.cshtml", model);
            }

            TempData["SuccessMessage"] = "Đã cập nhật các môn được phân công cho giáo viên.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> ValidateSubjectsAsync(
            List<int> subjectIds, List<int>? currentSubjectIds = null)
        {
            if (subjectIds.Count == 0)
            {
                ModelState.AddModelError(nameof(CreateTeacherViewModel.SubjectIds),
                    "Vui lòng chọn ít nhất một môn học.");
                return false;
            }

            currentSubjectIds ??= new List<int>();
            var validCount = await _context.Subjects.CountAsync(s =>
                subjectIds.Contains(s.Id) &&
                (s.Status == SubjectStatus.Active || currentSubjectIds.Contains(s.Id)));

            if (validCount != subjectIds.Count)
            {
                ModelState.AddModelError(nameof(CreateTeacherViewModel.SubjectIds),
                    "Môn học không tồn tại hoặc đã ngừng hoạt động. Chỉ được thêm môn đang hoạt động.");
                return false;
            }

            return true;
        }

        private async Task LoadSubjectsAsync(
            List<int>? selectedSubjectIds = null, List<int>? currentSubjectIds = null)
        {
            selectedSubjectIds ??= new List<int>();
            currentSubjectIds ??= new List<int>();
            var subjects = await _context.Subjects
                .AsNoTracking()
                .Where(s => s.Status == SubjectStatus.Active || currentSubjectIds.Contains(s.Id))
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewBag.Subjects = subjects.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Name + (s.Status == SubjectStatus.Inactive ? " (ngừng hoạt động)" : ""),
                Selected = selectedSubjectIds.Contains(s.Id)
            }).ToList();
        }


    }
}
