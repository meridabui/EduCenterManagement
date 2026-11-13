using EduCenterManagement.ViewModels.Admin;

namespace EduCenterManagement.Services.Admin;

public interface IAdminDashboardService
{
    Task<AdminDashboardViewModel> GetDashboardAsync();
}
