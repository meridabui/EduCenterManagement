using EduCenterManagement.Enums;

namespace EduCenterManagement.Services.Admin;

public interface IClassLifecycleService
{
    Task<ClassTransitionResult> ChangeAsync(int id, ClassStatus target);
}
