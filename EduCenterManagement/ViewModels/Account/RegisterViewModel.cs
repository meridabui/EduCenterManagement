using System.ComponentModel.DataAnnotations;

namespace EduCenterManagement.ViewModels.Account
{
    public class RegisterViewModel
    {
        // "Parent" hoặc "Student". Teacher chỉ do Admin tạo (BR02).
        [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
        public string Role { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [MaxLength(100, ErrorMessage = "Họ và tên tối đa 100 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [MinLength(8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự.")]
        [MaxLength(100, ErrorMessage = "Mật khẩu tối đa 100 ký tự.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
