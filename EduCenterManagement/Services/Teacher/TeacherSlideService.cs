using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Teacher;
using EduCenterManagement.ViewModels.Teacher.Slide;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.Services.Teacher
{
    public class TeacherSlideService : ITeacherSlideService
    {
        private readonly ITeacherSlideRepository _slideRepository;
        private readonly IWebHostEnvironment _webHostEnvironment;

        // Cho phép các định dạng tài liệu thông dụng
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".pptx", ".ppt", ".docx", ".doc", ".xlsx", ".xls", ".zip", ".rar", ".7z", ".txt", ".png", ".jpg", ".jpeg"
        };

        // Giới hạn dung lượng 50MB
        private const long MaxFileSizeBytes = 50 * 1024 * 1024;

        public TeacherSlideService(ITeacherSlideRepository slideRepository, IWebHostEnvironment webHostEnvironment)
        {
            _slideRepository = slideRepository;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<ClassSessionSlidePickerViewModel> GetClassSessionPickerDataAsync(int userId, int? selectedClassId, int? selectedSessionId)
        {
            var teacher = await _slideRepository.GetTeacherProfileByUserIdAsync(userId);
            var viewModel = new ClassSessionSlidePickerViewModel
            {
                TeacherName = teacher?.FullName ?? "Giáo viên",
                SelectedClassId = selectedClassId,
                SelectedSessionId = selectedSessionId
            };

            if (teacher == null) return viewModel;

            var classes = await _slideRepository.GetTeacherClassesAsync(teacher.Id);
            viewModel.Classes = classes.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.Name} ({c.Subject?.Name ?? "Môn học"})",
                Selected = selectedClassId.HasValue && c.Id == selectedClassId.Value
            }).ToList();

            int? targetClassId = selectedClassId ?? classes.FirstOrDefault()?.Id;
            if (targetClassId.HasValue)
            {
                viewModel.SelectedClassId = targetClassId.Value;
                var sessions = await _slideRepository.GetSessionsByClassIdAsync(targetClassId.Value, teacher.Id);
                viewModel.Sessions = sessions.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"Buổi {s.SessionNumber} ({s.SessionDate:dd/MM/yyyy} - {s.StartsAt:HH:mm} đến {s.EndsAt:HH:mm})",
                    Selected = selectedSessionId.HasValue && s.Id == selectedSessionId.Value
                }).ToList();
            }

            return viewModel;
        }

        public async Task<List<SelectListItem>> GetSessionsDropdownByClassIdAsync(int userId, int classId)
        {
            var teacher = await _slideRepository.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return new List<SelectListItem>();

            var sessions = await _slideRepository.GetSessionsByClassIdAsync(classId, teacher.Id);
            return sessions.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = $"Buổi {s.SessionNumber} ({s.SessionDate:dd/MM/yyyy} - {s.StartsAt:HH:mm} đến {s.EndsAt:HH:mm})"
            }).ToList();
        }

        public async Task<SessionSlideListViewModel?> GetSessionSlidesAsync(int userId, int sessionId)
        {
            var teacher = await _slideRepository.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var session = await _slideRepository.GetSessionWithClassByIdAsync(sessionId, teacher.Id);
            if (session == null) return null;

            var slides = await _slideRepository.GetSlidesBySessionIdAsync(sessionId);

            var viewModel = new SessionSlideListViewModel
            {
                SessionId = session.Id,
                SessionNumber = session.SessionNumber,
                SessionDate = session.SessionDate,
                StartsAt = session.StartsAt,
                EndsAt = session.EndsAt,
                ClassId = session.ClassId,
                ClassName = session.Class.Name,
                SubjectName = session.Class.Subject?.Name ?? "N/A",
                TeacherName = teacher.FullName,
                Slides = slides.Select(s => new SlideItemViewModel
                {
                    Id = s.Id,
                    Title = s.Title,
                    OriginalFileName = s.OriginalFileName,
                    FileUrl = s.FileUrl,
                    FileSize = s.FileSize,
                    MimeType = s.MimeType,
                    UploadedAt = s.UploadedAt
                }).ToList(),
                UploadInput = new UploadSlideInputModel
                {
                    SessionId = session.Id
                }
            };

            return viewModel;
        }

        public async Task<(bool Success, string Message)> UploadSlideAsync(int userId, UploadSlideInputModel model)
        {
            var teacher = await _slideRepository.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null)
            {
                return (false, "Không tìm thấy hồ sơ giáo viên.");
            }

            var session = await _slideRepository.GetSessionWithClassByIdAsync(model.SessionId, teacher.Id);
            if (session == null)
            {
                return (false, "Buổi học không tồn tại hoặc bạn không có quyền tải lên tài liệu cho buổi này.");
            }

            if (model.File == null || model.File.Length == 0)
            {
                return (false, "Vui lòng chọn tệp tin cần tải lên.");
            }

            if (model.File.Length > MaxFileSizeBytes)
            {
                return (false, "Dung lượng tệp vượt quá giới hạn tối đa cho phép (50MB).");
            }

            var extension = Path.GetExtension(model.File.FileName);
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                return (false, $"Định dạng tệp '{extension}' không được hỗ trợ. Vui lòng chọn tệp PDF, Word, PowerPoint, Excel hoặc nén (.zip, .rar).");
            }

            try
            {
                string webRoot = _webHostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                string relativeFolder = Path.Combine("uploads", "slides", session.Id.ToString());
                string absoluteFolder = Path.Combine(webRoot, relativeFolder);

                if (!Directory.Exists(absoluteFolder))
                {
                    Directory.CreateDirectory(absoluteFolder);
                }

                string storageKey = $"{Guid.NewGuid():N}{extension}";
                string absoluteFilePath = Path.Combine(absoluteFolder, storageKey);

                using (var stream = new FileStream(absoluteFilePath, FileMode.Create))
                {
                    await model.File.CopyToAsync(stream);
                }

                string fileUrl = $"/{relativeFolder.Replace('\\', '/')}/{storageKey}";

                var slide = new Slide
                {
                    SessionId = session.Id,
                    UploadedByTeacherId = teacher.Id,
                    Title = model.Title.Trim(),
                    FileUrl = fileUrl,
                    OriginalFileName = Path.GetFileName(model.File.FileName),
                    StorageKey = storageKey,
                    MimeType = string.IsNullOrWhiteSpace(model.File.ContentType) ? "application/octet-stream" : model.File.ContentType,
                    FileSize = model.File.Length,
                    UploadedAt = DateTime.UtcNow
                };

                await _slideRepository.AddSlideAsync(slide);

                return (true, "Tải lên tài liệu / slide thành công!");
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi trong quá trình lưu trữ file: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteSlideAsync(int userId, int slideId)
        {
            var teacher = await _slideRepository.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null)
            {
                return (false, "Không tìm thấy hồ sơ giáo viên.");
            }

            var slide = await _slideRepository.GetSlideByIdAsync(slideId, teacher.Id);
            if (slide == null)
            {
                return (false, "Tài liệu không tồn tại hoặc bạn không có quyền xóa.");
            }

            try
            {
                // Xóa file vật lý trên đĩa nếu tồn tại
                if (!string.IsNullOrEmpty(slide.StorageKey))
                {
                    string webRoot = _webHostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string filePath = Path.Combine(webRoot, "uploads", "slides", slide.SessionId.ToString(), slide.StorageKey);

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }

                await _slideRepository.DeleteSlideAsync(slide);
                return (true, "Đã xóa tài liệu thành công!");
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi xóa file: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? PhysicalPath, string? OriginalFileName, string? MimeType)> GetSlideFileForDownloadAsync(int userId, int slideId)
        {
            var teacher = await _slideRepository.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, null, null, null);

            var slide = await _slideRepository.GetSlideByIdAsync(slideId, teacher.Id);
            if (slide == null || string.IsNullOrEmpty(slide.StorageKey)) return (false, null, null, null);

            string webRoot = _webHostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string filePath = Path.Combine(webRoot, "uploads", "slides", slide.SessionId.ToString(), slide.StorageKey);

            if (!File.Exists(filePath))
            {
                return (false, null, null, null);
            }

            return (true, filePath, slide.OriginalFileName ?? Path.GetFileName(filePath), slide.MimeType ?? "application/octet-stream");
        }
    }
}
