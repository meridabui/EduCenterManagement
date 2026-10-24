
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class SubjectsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubjectsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Subjects
        public async Task<IActionResult> Index()
        {
            var subjects = await _context.Subjects
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();

            return View("~/Views/Admin/Subjects/Index.cshtml", subjects);
        }

        // GET: /Subjects/Details/1
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var subject = await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id.Value);

            if (subject == null)
                return NotFound();

            return View("~/Views/Admin/Subjects/Details.cshtml", subject);
        }

        // GET: /Subjects/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(
                "~/Views/Admin/Subjects/Create.cshtml",
                new SubjectViewModel());
        }

        // POST: /Subjects/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubjectViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/Subjects/Create.cshtml", model);
            }

            var name = model.Name.Trim();

            bool exists = await _context.Subjects
                .AnyAsync(s => s.Name == name);

            if (exists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Tên môn học đã tồn tại.");

                return View(
                    "~/Views/Admin/Subjects/Create.cshtml", model);
            }

            var now = DateTime.UtcNow;

            var subject = new Subject
            {
                Name = name,
                Description = model.Description?.Trim(),
                Status = model.Status,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Subjects.Add(subject);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể lưu môn học. Vui lòng kiểm tra dữ liệu.");

                return View(
                    "~/Views/Admin/Subjects/Create.cshtml", model);
            }

            TempData["SuccessMessage"] = "Thêm môn học thành công.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Subjects/Edit/1
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var subject = await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id.Value);

            if (subject == null)
                return NotFound();

            var model = new SubjectViewModel
            {
                Id = subject.Id,
                Name = subject.Name,
                Description = subject.Description,
                Status = subject.Status
            };

            return View("~/Views/Admin/Subjects/Edit.cshtml", model);
        }

        // POST: /Subjects/Edit/1
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            SubjectViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/Subjects/Edit.cshtml", model);
            }

            var name = model.Name.Trim();

            bool exists = await _context.Subjects
                .AnyAsync(s => s.Name == name && s.Id != id);

            if (exists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Tên môn học đã được sử dụng.");

                return View("~/Views/Admin/Subjects/Edit.cshtml", model);
            }

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subject == null)
                return NotFound();

            subject.Name = name;
            subject.Description = model.Description?.Trim();
            subject.Status = model.Status;
            subject.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể cập nhật môn học. Vui lòng thử lại.");

                return View("~/Views/Admin/Subjects/Edit.cshtml", model);
            }

            TempData["SuccessMessage"] = "Cập nhật môn học thành công.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Subjects/Delete/1
        // Hiển thị trang xác nhận Soft Delete
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var subject = await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id.Value);

            if (subject == null)
                return NotFound();

            return View("~/Views/Admin/Subjects/Delete.cshtml", subject);
        }

        // POST: /Subjects/Delete/1
        // Soft Delete: chuyển trạng thái sang Inactive
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var subject = await _context.Subjects
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subject == null)
                return NotFound();

            if (subject.Status == SubjectStatus.Inactive)
            {
                TempData["ErrorMessage"] =
                    "Môn học này đã ngừng hoạt động.";

                return RedirectToAction(nameof(Index));
            }

            subject.Status = SubjectStatus.Inactive;
            subject.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Không thể ngừng kích hoạt môn học. Vui lòng thử lại.";

                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                "Đã ngừng kích hoạt môn học thành công.";

            return RedirectToAction(nameof(Index));
        }
    }
}
