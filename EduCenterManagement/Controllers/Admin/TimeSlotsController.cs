
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
    public class TimeSlotsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TimeSlotsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /TimeSlots
        public async Task<IActionResult> Index()
        {
            var timeSlots = await _context.TimeSlots
                .AsNoTracking()
                .OrderBy(t => t.StartTime)
                .ToListAsync();

            return View("~/Views/Admin/TimeSlots/Index.cshtml", timeSlots);
        }

        // GET: /TimeSlots/Details/1
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var timeSlot = await _context.TimeSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id.Value);

            if (timeSlot == null)
                return NotFound();

            return View("~/Views/Admin/TimeSlots/Details.cshtml", timeSlot);
        }

        // GET: /TimeSlots/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(
                "~/Views/Admin/TimeSlots/Create.cshtml",
                new TimeSlotViewModel());
        }

        // POST: /TimeSlots/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TimeSlotViewModel model)
        {
            if (model.StartTime >= model.EndTime)
            {
                ModelState.AddModelError(
                    nameof(model.EndTime),
                    "Giờ kết thúc phải sau giờ bắt đầu.");
            }

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/TimeSlots/Create.cshtml", model);
            }

            var name = model.Name.Trim();

            bool nameExists = await _context.TimeSlots
                .AnyAsync(t => t.Name == name);

            if (nameExists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Tên khung giờ đã tồn tại.");

                return View(
                    "~/Views/Admin/TimeSlots/Create.cshtml", model);
            }

            // Không cho tạo khung giờ trùng hoặc chồng lấn.
            bool overlaps = await _context.TimeSlots.AnyAsync(t =>
                model.StartTime < t.EndTime &&
                model.EndTime > t.StartTime);

            if (overlaps)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Khung giờ mới bị trùng hoặc chồng lấn với khung giờ hiện có.");

                return View(
                    "~/Views/Admin/TimeSlots/Create.cshtml", model);
            }

            var now = DateTime.UtcNow;

            var timeSlot = new TimeSlot
            {
                Name = name,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.TimeSlots.Add(timeSlot);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể lưu khung giờ. Vui lòng kiểm tra dữ liệu.");

                return View(
                    "~/Views/Admin/TimeSlots/Create.cshtml", model);
            }

            TempData["SuccessMessage"] = "Thêm khung giờ thành công.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /TimeSlots/Edit/1
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var timeSlot = await _context.TimeSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id.Value);

            if (timeSlot == null)
                return NotFound();

            var model = new TimeSlotViewModel
            {
                Id = timeSlot.Id,
                Name = timeSlot.Name,
                StartTime = timeSlot.StartTime,
                EndTime = timeSlot.EndTime
            };

            return View("~/Views/Admin/TimeSlots/Edit.cshtml", model);
        }

        // POST: /TimeSlots/Edit/1
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            TimeSlotViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (model.StartTime >= model.EndTime)
            {
                ModelState.AddModelError(
                    nameof(model.EndTime),
                    "Giờ kết thúc phải sau giờ bắt đầu.");
            }

            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/TimeSlots/Edit.cshtml", model);
            }

            var name = model.Name.Trim();

            bool nameExists = await _context.TimeSlots
                .AnyAsync(t => t.Name == name && t.Id != id);

            if (nameExists)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Tên khung giờ đã được sử dụng.");

                return View("~/Views/Admin/TimeSlots/Edit.cshtml", model);
            }

            bool overlaps = await _context.TimeSlots.AnyAsync(t =>
                t.Id != id &&
                model.StartTime < t.EndTime &&
                model.EndTime > t.StartTime);

            if (overlaps)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Khung giờ bị trùng hoặc chồng lấn với khung giờ khác.");

                return View("~/Views/Admin/TimeSlots/Edit.cshtml", model);
            }

            var timeSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(t => t.Id == id);

            if (timeSlot == null)
                return NotFound();

            timeSlot.Name = name;
            timeSlot.StartTime = model.StartTime;
            timeSlot.EndTime = model.EndTime;
            timeSlot.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể cập nhật khung giờ. Vui lòng thử lại.");

                return View("~/Views/Admin/TimeSlots/Edit.cshtml", model);
            }

            TempData["SuccessMessage"] = "Cập nhật khung giờ thành công.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /TimeSlots/Delete/1
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var timeSlot = await _context.TimeSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id.Value);

            if (timeSlot == null)
                return NotFound();

            return View("~/Views/Admin/TimeSlots/Delete.cshtml", timeSlot);
        }


        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var timeSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(t => t.Id == id);

            if (timeSlot == null)
                return NotFound();

            if (timeSlot.Status == TimeSlotStatus.Inactive)
            {
                TempData["ErrorMessage"] =
                    "Khung giờ này đã ngừng hoạt động.";

                return RedirectToAction(nameof(Index));
            }

            timeSlot.Status = TimeSlotStatus.Inactive;
            timeSlot.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Đã ngừng kích hoạt khung giờ thành công.";

            return RedirectToAction(nameof(Index));
        }


        // POST: /TimeSlots/Reactivate/1
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(int id)
        {
            var timeSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(t => t.Id == id);

            if (timeSlot == null)
                return NotFound();

            if (timeSlot.Status == TimeSlotStatus.Active)
            {
                TempData["ErrorMessage"] =
                    "Khung giờ này đang hoạt động.";

                return RedirectToAction(nameof(Index));
            }

            // Không cho kích hoạt nếu giờ bị chồng lấn
            // với một khung giờ đang hoạt động.
            bool overlaps = await _context.TimeSlots.AnyAsync(t =>
                t.Id != id &&
                t.Status == TimeSlotStatus.Active &&
                timeSlot.StartTime < t.EndTime &&
                timeSlot.EndTime > t.StartTime);

            if (overlaps)
            {
                TempData["ErrorMessage"] =
                    "Không thể kích hoạt lại vì khung giờ bị chồng lấn " +
                    "với một khung giờ đang hoạt động.";

                return RedirectToAction(nameof(Index));
            }

            timeSlot.Status = TimeSlotStatus.Active;
            timeSlot.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Kích hoạt lại khung giờ thành công.";

            return RedirectToAction(nameof(Index));
        }

        // POST: /TimeSlots/GenerateDefaultSlots
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateDefaultSlots()
        {
            var defaultSlots = new List<(string Name, TimeOnly StartTime, TimeOnly EndTime)>
            {
                ("Slot 1", new TimeOnly(7, 0), new TimeOnly(9, 0)),
                ("Slot 2", new TimeOnly(9, 0), new TimeOnly(11, 0)),
                ("Slot 3", new TimeOnly(13, 0), new TimeOnly(15, 0)),
                ("Slot 4", new TimeOnly(15, 0), new TimeOnly(17, 0)),
                ("Slot 5", new TimeOnly(17, 0), new TimeOnly(19, 0)),
                ("Slot 6", new TimeOnly(19, 0), new TimeOnly(21, 0))
            };

            var existingSlots = await _context.TimeSlots.ToListAsync();
            var now = DateTime.UtcNow;
            var addedSlots = new List<TimeSlot>();
            var skippedNames = new List<string>();

            foreach (var slot in defaultSlots)
            {
                // Kiểm tra nếu tên khung giờ đã tồn tại
                bool nameExists = existingSlots.Any(e =>
                    string.Equals(e.Name, slot.Name, StringComparison.OrdinalIgnoreCase));

                // Kiểm tra nếu thời gian bị trùng hoặc chồng lấn với khung giờ đang active
                bool overlaps = existingSlots.Any(e =>
                    e.Status == TimeSlotStatus.Active &&
                    slot.StartTime < e.EndTime &&
                    slot.EndTime > e.StartTime);

                if (nameExists || overlaps)
                {
                    skippedNames.Add(slot.Name);
                    continue;
                }

                var newSlot = new TimeSlot
                {
                    Name = slot.Name,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Status = TimeSlotStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                addedSlots.Add(newSlot);
                existingSlots.Add(newSlot);
            }

            if (addedSlots.Count == 0)
            {
                TempData["ErrorMessage"] =
                    "Tất cả 6 khung giờ mặc định (Slot 1 - Slot 6) đã tồn tại hoặc bị trùng lặp thời gian.";
                return RedirectToAction(nameof(Index));
            }

            _context.TimeSlots.AddRange(addedSlots);
            await _context.SaveChangesAsync();

            if (skippedNames.Count > 0)
            {
                TempData["SuccessMessage"] =
                    $"Đã khởi tạo thành công {addedSlots.Count} khung giờ mặc định. (Bỏ qua: {string.Join(", ", skippedNames)} do đã tồn tại hoặc trùng lấn).";
            }
            else
            {
                TempData["SuccessMessage"] =
                    $"Đã tự động khởi tạo thành công tất cả 6 khung giờ mặc định (Slot 1 đến Slot 6).";
            }

            return RedirectToAction(nameof(Index));
        }

    }
}
